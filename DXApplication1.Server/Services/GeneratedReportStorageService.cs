#nullable enable
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DXApplication1.Services
{
    /// <summary>
    /// Azure Blob Storage implementation for storing generated per-pupil reports.
    /// Uses blob tags for efficient filtering by OrgId and UserExternalId.
    /// </summary>
    public class GeneratedReportStorageService : IGeneratedReportStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<GeneratedReportStorageService> _logger;
        private readonly string _containerName;

        // Regex pattern for sanitizing log values - removes newlines and control characters
        private static readonly Regex LogSanitizePattern = new Regex(@"[\r\n\t]+", RegexOptions.Compiled);

        public GeneratedReportStorageService(IConfiguration configuration, ILogger<GeneratedReportStorageService> logger)
        {
            _logger = logger;

            var connectionString = configuration["AzureStorage:ConnectionString"];
            _containerName = configuration["AzureStorage:GeneratedReportsContainerName"] ?? "generated-reports";

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Azure Storage connection string is not configured. Set 'AzureStorage:ConnectionString' in configuration.");
            }

            try
            {
                _blobServiceClient = new BlobServiceClient(connectionString);
                _containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                _containerClient.CreateIfNotExists(PublicAccessType.None);
                _logger.LogInformation(
                    "Generated Report Storage service initialized successfully. Container: {ContainerName}",
                    _containerName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize Generated Report Storage service.", ex);
            }
        }

        /// <summary>
        /// Sanitizes a string for safe logging by removing newlines and control characters.
        /// </summary>
        private static string SanitizeForLog(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return LogSanitizePattern.Replace(value, " ");
        }

        /// <summary>
        /// Generates a unique blob name for a generated report.
        /// Format: {orgId}/{userExternalId}/{reportName}_{learnerExternalId}_{timestamp}.{format}
        /// </summary>
        private static string GenerateBlobName(
            string orgId,
            string userExternalId,
            string reportName,
            string learnerExternalId,
            string format)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var safeReportName = SanitizeFileName(reportName);
            var safeLearnerExternalId = SanitizeFileName(learnerExternalId);
            var safeOrgId = SanitizeFileName(orgId);
            var safeUserExternalId = SanitizeFileName(userExternalId);

            return $"{safeOrgId}/{safeUserExternalId}/{safeReportName}_{safeLearnerExternalId}_{timestamp}.{format}";
        }

        /// <summary>
        /// Extracts the report ID from a blob name (blob name without extension).
        /// </summary>
        private static string ExtractReportId(string blobName)
        {
            return Path.GetFileNameWithoutExtension(blobName);
        }

        /// <summary>
        /// Sanitizes a string for use in blob names.
        /// </summary>
        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "unknown";

            // Replace invalid characters with underscores
            var invalidChars = Path.GetInvalidFileNameChars();
            var result = input;
            foreach (var c in invalidChars)
            {
                result = result.Replace(c, '_');
            }

            // Also replace spaces and some special chars that might cause issues
            result = result.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');

            return result;
        }

        public async Task<GeneratedReportMetadata> UploadGeneratedReportAsync(
            Stream reportStream,
            string reportName,
            string learnerExternalId,
            string learnerName,
            string orgId,
            string userExternalId,
            string format = "pdf")
        {
            var blobName = GenerateBlobName(orgId, userExternalId, reportName, learnerExternalId, format);
            var blobClient = _containerClient.GetBlobClient(blobName);

            try
            {
                reportStream.Position = 0;

                // Set content type based on format
                var contentType = format.ToLowerInvariant() switch
                {
                    "pdf" => "application/pdf",
                    "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    _ => "application/octet-stream"
                };

                // Upload with blob tags for efficient filtering
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Tags = new Dictionary<string, string>
                    {
                        ["OrgId"] = SanitizeTagValue(orgId),
                        ["UserExternalId"] = SanitizeTagValue(userExternalId),
                        ["ReportName"] = SanitizeTagValue(reportName),
                        ["LearnerExternalId"] = SanitizeTagValue(learnerExternalId),
                        ["Format"] = format,
                        ["GeneratedAt"] = DateTime.UtcNow.ToString("O")
                    }
                };

                await blobClient.UploadAsync(reportStream, uploadOptions);

                var metadata = new GeneratedReportMetadata
                {
                    Id = ExtractReportId(blobName),
                    ReportName = reportName,
                    LearnerExternalId = learnerExternalId,
                    LearnerName = learnerName,
                    OrgId = orgId,
                    UserExternalId = userExternalId,
                    Format = format,
                    GeneratedAt = DateTime.UtcNow,
                    FileSizeBytes = reportStream.Length,
                    BlobName = blobName
                };

                _logger.LogInformation(
                    "Generated report uploaded successfully: {BlobName} for learner {LearnerExternalId}",
                    SanitizeForLog(blobName),
                    SanitizeForLog(learnerExternalId));

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to upload generated report: {ReportName} for learner {LearnerExternalId}",
                    SanitizeForLog(reportName),
                    SanitizeForLog(learnerExternalId));
                throw;
            }
        }

        /// <summary>
        /// Sanitizes a tag value for Azure Blob Storage tags.
        /// Tags have restrictions: alphanumeric, +, -, ., :, =, _, max 256 chars.
        /// </summary>
        private static string SanitizeTagValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";

            // Replace invalid characters
            var result = Regex.Replace(value, @"[^a-zA-Z0-9+\-.:=_]", "_");

            // Truncate to 256 characters (Azure tag value limit)
            if (result.Length > 256)
                result = result.Substring(0, 256);

            return result;
        }

        public async Task<List<GeneratedReportMetadata>> ListUserReportsAsync(string orgId, string userExternalId)
        {
            var reports = new List<GeneratedReportMetadata>();

            try
            {
                // Use blob tags to filter efficiently
                // Query format: "OrgId" = 'value' AND "UserExternalId" = 'value'
                var sanitizedOrgId = SanitizeTagValue(orgId);
                var sanitizedUserExternalId = SanitizeTagValue(userExternalId);
                var tagQuery = $"\"OrgId\" = '{sanitizedOrgId}' AND \"UserExternalId\" = '{sanitizedUserExternalId}'";

                await foreach (var taggedBlob in _blobServiceClient.FindBlobsByTagsAsync(tagQuery))
                {
                    // Only include blobs from our container
                    if (!taggedBlob.BlobContainerName.Equals(_containerName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var containerClient = _blobServiceClient.GetBlobContainerClient(taggedBlob.BlobContainerName);
                    var blobClient = containerClient.GetBlobClient(taggedBlob.BlobName);

                    try
                    {
                        var properties = await blobClient.GetPropertiesAsync();
                        var tags = await blobClient.GetTagsAsync();

                        var metadata = new GeneratedReportMetadata
                        {
                            Id = ExtractReportId(taggedBlob.BlobName),
                            BlobName = taggedBlob.BlobName,
                            OrgId = orgId,
                            UserExternalId = userExternalId,
                            FileSizeBytes = properties.Value.ContentLength,
                            GeneratedAt = properties.Value.CreatedOn?.UtcDateTime ?? DateTime.UtcNow
                        };

                        // Extract additional metadata from tags
                        if (tags.Value.Tags.TryGetValue("ReportName", out var reportName))
                            metadata.ReportName = reportName;
                        if (tags.Value.Tags.TryGetValue("LearnerExternalId", out var learnerId))
                            metadata.LearnerExternalId = learnerId;
                        if (tags.Value.Tags.TryGetValue("Format", out var format))
                            metadata.Format = format;

                        reports.Add(metadata);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        // Blob was deleted between tag query and property fetch, skip it
                        continue;
                    }
                }

                _logger.LogInformation(
                    "Listed {Count} generated reports for user {UserExternalId} in org {OrgId}",
                    reports.Count,
                    SanitizeForLog(userExternalId),
                    SanitizeForLog(orgId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to list generated reports for user {UserExternalId} in org {OrgId}",
                    SanitizeForLog(userExternalId),
                    SanitizeForLog(orgId));
                throw;
            }

            return reports;
        }

        public async Task<(Stream? Stream, GeneratedReportMetadata? Metadata)> DownloadReportAsync(
            string reportId,
            string orgId,
            string userExternalId)
        {
            try
            {
                // Use blob tags to find the report and validate access
                var sanitizedOrgId = SanitizeTagValue(orgId);
                var sanitizedUserExternalId = SanitizeTagValue(userExternalId);

                // Search for blob by tags including the report ID pattern in the name
                var tagQuery = $"\"OrgId\" = '{sanitizedOrgId}' AND \"UserExternalId\" = '{sanitizedUserExternalId}'";

                await foreach (var taggedBlob in _blobServiceClient.FindBlobsByTagsAsync(tagQuery))
                {
                    if (!taggedBlob.BlobContainerName.Equals(_containerName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var blobReportId = ExtractReportId(taggedBlob.BlobName);
                    if (!blobReportId.Equals(reportId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Found the matching blob
                    var containerClient = _blobServiceClient.GetBlobContainerClient(taggedBlob.BlobContainerName);
                    var blobClient = containerClient.GetBlobClient(taggedBlob.BlobName);

                    var memoryStream = new MemoryStream();
                    await blobClient.DownloadToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var properties = await blobClient.GetPropertiesAsync();
                    var tags = await blobClient.GetTagsAsync();

                    var metadata = new GeneratedReportMetadata
                    {
                        Id = reportId,
                        BlobName = taggedBlob.BlobName,
                        OrgId = orgId,
                        UserExternalId = userExternalId,
                        FileSizeBytes = properties.Value.ContentLength,
                        GeneratedAt = properties.Value.CreatedOn?.UtcDateTime ?? DateTime.UtcNow
                    };

                    if (tags.Value.Tags.TryGetValue("ReportName", out var reportName))
                        metadata.ReportName = reportName;
                    if (tags.Value.Tags.TryGetValue("LearnerExternalId", out var learnerId))
                        metadata.LearnerExternalId = learnerId;
                    if (tags.Value.Tags.TryGetValue("Format", out var format))
                        metadata.Format = format;

                    _logger.LogInformation(
                        "Downloaded generated report: {ReportId} for user {UserExternalId}",
                        SanitizeForLog(reportId),
                        SanitizeForLog(userExternalId));

                    return (memoryStream, metadata);
                }

                _logger.LogWarning(
                    "Generated report not found or access denied: {ReportId} for user {UserExternalId} in org {OrgId}",
                    SanitizeForLog(reportId),
                    SanitizeForLog(userExternalId),
                    SanitizeForLog(orgId));

                return (null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to download generated report: {ReportId}",
                    SanitizeForLog(reportId));
                throw;
            }
        }

        public async Task<bool> DeleteReportAsync(string reportId, string orgId, string userExternalId)
        {
            try
            {
                var sanitizedOrgId = SanitizeTagValue(orgId);
                var sanitizedUserExternalId = SanitizeTagValue(userExternalId);
                var tagQuery = $"\"OrgId\" = '{sanitizedOrgId}' AND \"UserExternalId\" = '{sanitizedUserExternalId}'";

                await foreach (var taggedBlob in _blobServiceClient.FindBlobsByTagsAsync(tagQuery))
                {
                    if (!taggedBlob.BlobContainerName.Equals(_containerName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var blobReportId = ExtractReportId(taggedBlob.BlobName);
                    if (!blobReportId.Equals(reportId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var containerClient = _blobServiceClient.GetBlobContainerClient(taggedBlob.BlobContainerName);
                    var blobClient = containerClient.GetBlobClient(taggedBlob.BlobName);

                    await blobClient.DeleteIfExistsAsync();

                    _logger.LogInformation(
                        "Deleted generated report: {ReportId} for user {UserExternalId}",
                        SanitizeForLog(reportId),
                        SanitizeForLog(userExternalId));

                    return true;
                }

                _logger.LogWarning(
                    "Generated report not found for deletion: {ReportId}",
                    SanitizeForLog(reportId));

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to delete generated report: {ReportId}",
                    SanitizeForLog(reportId));
                throw;
            }
        }
    }
}
