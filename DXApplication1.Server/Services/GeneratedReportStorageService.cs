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
    /// Uses organization-based containers (one container per OrgId) with unique blob names.
    /// Blob structure: {containerPrefix}-{orgId} / {userExternalId}/{reportName}_{learnerExternalId}_{timestamp}.{format}
    /// </summary>
    public class GeneratedReportStorageService : IGeneratedReportStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<GeneratedReportStorageService> _logger;
        private readonly string _containerPrefix;

        // Regex pattern for sanitizing log values - removes newlines and control characters
        private static readonly Regex LogSanitizePattern = new Regex(@"[\r\n\t]+", RegexOptions.Compiled);

        public GeneratedReportStorageService(IConfiguration configuration, ILogger<GeneratedReportStorageService> logger)
        {
            _logger = logger;

            var connectionString = configuration["AzureStorage:ConnectionString"];
            _containerPrefix = configuration["AzureStorage:GeneratedReportsContainerPrefix"] ?? "org";

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Azure Storage connection string is not configured. Set 'AzureStorage:ConnectionString' in configuration.");
            }

            try
            {
                _blobServiceClient = new BlobServiceClient(connectionString);
                _logger.LogInformation(
                    "Generated Report Storage service initialized successfully. Container prefix: {ContainerPrefix}",
                    _containerPrefix);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize Generated Report Storage service.", ex);
            }
        }

        /// <summary>
        /// Gets or creates a container for the specified organization.
        /// Container name format: {prefix}-{sanitizedOrgId}
        /// </summary>
        private async Task<BlobContainerClient> GetOrCreateOrgContainerAsync(string orgId)
        {
            var containerName = GetContainerName(orgId);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            return containerClient;
        }

        /// <summary>
        /// Gets a container client for the specified organization (without creating).
        /// </summary>
        private BlobContainerClient GetOrgContainer(string orgId)
        {
            var containerName = GetContainerName(orgId);
            return _blobServiceClient.GetBlobContainerClient(containerName);
        }

        /// <summary>
        /// Generates the container name for an organization.
        /// Azure container names must be lowercase, 3-63 characters, start with letter or number.
        /// </summary>
        private string GetContainerName(string orgId)
        {
            var safeOrgId = SanitizeContainerName(orgId);
            return $"{_containerPrefix}-{safeOrgId}".ToLowerInvariant();
        }

        /// <summary>
        /// Sanitizes a string for use in Azure container names.
        /// Container names can only contain lowercase letters, numbers, and hyphens.
        /// </summary>
        private static string SanitizeContainerName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "unknown";

            // Replace invalid characters with hyphens, convert to lowercase
            var result = Regex.Replace(input.ToLowerInvariant(), @"[^a-z0-9-]", "-");

            // Remove consecutive hyphens
            result = Regex.Replace(result, @"-+", "-");

            // Trim hyphens from start/end
            result = result.Trim('-');

            // Ensure minimum length of 3 and max of 63 (minus prefix)
            if (result.Length < 1)
                result = "unknown";
            if (result.Length > 50)
                result = result.Substring(0, 50);

            return result;
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
        /// Format: {userExternalId}/{reportName}_{learnerExternalId}_{timestamp}.{format}
        /// </summary>
        private static string GenerateBlobName(
            string userExternalId,
            string reportName,
            string learnerExternalId,
            string format)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var safeReportName = SanitizeFileName(reportName);
            var safeLearnerExternalId = SanitizeFileName(learnerExternalId);
            var safeUserExternalId = SanitizeFileName(userExternalId);

            return $"{safeUserExternalId}/{safeReportName}_{safeLearnerExternalId}_{timestamp}.{format}";
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
            var containerClient = await GetOrCreateOrgContainerAsync(orgId);
            var blobName = GenerateBlobName(userExternalId, reportName, learnerExternalId, format);
            var blobClient = containerClient.GetBlobClient(blobName);

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

                // Upload with metadata (stored in blob metadata, not tags)
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                    Metadata = new Dictionary<string, string>
                    {
                        ["OrgId"] = orgId,
                        ["UserExternalId"] = userExternalId,
                        ["ReportName"] = reportName,
                        ["LearnerExternalId"] = learnerExternalId,
                        ["LearnerName"] = learnerName,
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
                    "Generated report uploaded successfully: {BlobName} in container {ContainerName} for learner {LearnerExternalId}",
                    SanitizeForLog(blobName),
                    SanitizeForLog(containerClient.Name),
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
        /// Sanitizes a metadata value for Azure Blob Storage.
        /// Metadata values should be ASCII-safe strings.
        /// </summary>
        private static string SanitizeMetadataValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";

            // Replace non-ASCII and control characters
            var result = Regex.Replace(value, @"[^\x20-\x7E]", "_");

            // Truncate to reasonable length
            if (result.Length > 1024)
                result = result.Substring(0, 1024);

            return result;
        }

        public async Task<List<GeneratedReportMetadata>> ListUserReportsAsync(string orgId, string userExternalId)
        {
            var reports = new List<GeneratedReportMetadata>();

            try
            {
                var containerClient = GetOrgContainer(orgId);

                // Check if container exists
                if (!await containerClient.ExistsAsync())
                {
                    _logger.LogInformation(
                        "No container found for org {OrgId}, returning empty list",
                        SanitizeForLog(orgId));
                    return reports;
                }

                // List blobs with the user's prefix (blobs are stored as {userExternalId}/...)
                var prefix = $"{SanitizeFileName(userExternalId)}/";

                await foreach (var blobItem in containerClient.GetBlobsAsync(
                    traits: BlobTraits.Metadata,
                    prefix: prefix))
                {
                    var metadata = new GeneratedReportMetadata
                    {
                        Id = ExtractReportId(blobItem.Name),
                        BlobName = blobItem.Name,
                        OrgId = orgId,
                        UserExternalId = userExternalId,
                        FileSizeBytes = blobItem.Properties.ContentLength ?? 0,
                        GeneratedAt = blobItem.Properties.CreatedOn?.UtcDateTime ?? DateTime.UtcNow
                    };

                    // Extract additional metadata from blob metadata
                    if (blobItem.Metadata != null)
                    {
                        if (blobItem.Metadata.TryGetValue("ReportName", out var reportName))
                            metadata.ReportName = reportName;
                        if (blobItem.Metadata.TryGetValue("LearnerExternalId", out var learnerId))
                            metadata.LearnerExternalId = learnerId;
                        if (blobItem.Metadata.TryGetValue("LearnerName", out var learnerName))
                            metadata.LearnerName = learnerName;
                        if (blobItem.Metadata.TryGetValue("Format", out var format))
                            metadata.Format = format;
                    }

                    reports.Add(metadata);
                }

                _logger.LogInformation(
                    "Listed {Count} generated reports for user {UserExternalId} in org {OrgId}",
                    reports.Count,
                    SanitizeForLog(userExternalId),
                    SanitizeForLog(orgId));
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Container doesn't exist, return empty list
                _logger.LogInformation(
                    "Container not found for org {OrgId}, returning empty list",
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
                var containerClient = GetOrgContainer(orgId);

                // Check if container exists
                if (!await containerClient.ExistsAsync())
                {
                    _logger.LogWarning(
                        "Container not found for org {OrgId} when downloading report {ReportId}",
                        SanitizeForLog(orgId),
                        SanitizeForLog(reportId));
                    return (null, null);
                }

                // List blobs with the user's prefix to find the matching report
                var prefix = $"{SanitizeFileName(userExternalId)}/";

                await foreach (var blobItem in containerClient.GetBlobsAsync(
                    traits: BlobTraits.Metadata,
                    prefix: prefix))
                {
                    var blobReportId = ExtractReportId(blobItem.Name);
                    if (!blobReportId.Equals(reportId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Found the matching blob
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);

                    var memoryStream = new MemoryStream();
                    await blobClient.DownloadToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var metadata = new GeneratedReportMetadata
                    {
                        Id = reportId,
                        BlobName = blobItem.Name,
                        OrgId = orgId,
                        UserExternalId = userExternalId,
                        FileSizeBytes = blobItem.Properties.ContentLength ?? 0,
                        GeneratedAt = blobItem.Properties.CreatedOn?.UtcDateTime ?? DateTime.UtcNow
                    };

                    // Extract additional metadata
                    if (blobItem.Metadata != null)
                    {
                        if (blobItem.Metadata.TryGetValue("ReportName", out var reportName))
                            metadata.ReportName = reportName;
                        if (blobItem.Metadata.TryGetValue("LearnerExternalId", out var learnerId))
                            metadata.LearnerExternalId = learnerId;
                        if (blobItem.Metadata.TryGetValue("LearnerName", out var learnerName))
                            metadata.LearnerName = learnerName;
                        if (blobItem.Metadata.TryGetValue("Format", out var format))
                            metadata.Format = format;
                    }

                    _logger.LogInformation(
                        "Downloaded generated report: {ReportId} for user {UserExternalId} in org {OrgId}",
                        SanitizeForLog(reportId),
                        SanitizeForLog(userExternalId),
                        SanitizeForLog(orgId));

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
                var containerClient = GetOrgContainer(orgId);

                // Check if container exists
                if (!await containerClient.ExistsAsync())
                {
                    _logger.LogWarning(
                        "Container not found for org {OrgId} when deleting report {ReportId}",
                        SanitizeForLog(orgId),
                        SanitizeForLog(reportId));
                    return false;
                }

                // List blobs with the user's prefix to find the matching report
                var prefix = $"{SanitizeFileName(userExternalId)}/";

                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    var blobReportId = ExtractReportId(blobItem.Name);
                    if (!blobReportId.Equals(reportId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    await blobClient.DeleteIfExistsAsync();

                    _logger.LogInformation(
                        "Deleted generated report: {ReportId} for user {UserExternalId} in org {OrgId}",
                        SanitizeForLog(reportId),
                        SanitizeForLog(userExternalId),
                        SanitizeForLog(orgId));

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
