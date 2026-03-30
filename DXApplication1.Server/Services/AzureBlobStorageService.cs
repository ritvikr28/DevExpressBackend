#nullable enable
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace DXApplication1.Services
{
    public interface IAzureBlobStorageService
    {
        // Synchronous methods for compatibility with DevExpress ReportStorageWebExtension
        bool UploadReportSync(string reportName, Stream reportStream);
        Stream? DownloadReportSync(string reportName);
        List<string> ListReportsSync();
    }

    public class AzureBlobStorageService : IAzureBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<AzureBlobStorageService> _logger;

        public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
        {
            _logger = logger;

            var connectionString = configuration["AzureStorage:ConnectionString"];
            var containerName = configuration["AzureStorage:ContainerName"] ?? "reports";

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Azure Storage connection string is not configured. Set 'AzureStorage:ConnectionString' in configuration.");
            }

            try
            {
                var blobServiceClient = new BlobServiceClient(connectionString);
                _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                _containerClient.CreateIfNotExists(PublicAccessType.None);
                _logger.LogInformation("Azure Blob Storage service initialized successfully. Container: {ContainerName}", containerName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize Azure Blob Storage service.", ex);
            }
        }

        private static string GetBlobName(string reportName)
        {
            var safeReportName = Path.GetFileNameWithoutExtension(reportName);
            return $"{safeReportName}.repx";
        }

        // Synchronous methods for compatibility with DevExpress ReportStorageWebExtension
        // These methods use synchronous Azure SDK calls to avoid deadlocks

        public bool UploadReportSync(string reportName, Stream reportStream)
        {
            try
            {
                var blobName = GetBlobName(reportName);
                var blobClient = _containerClient.GetBlobClient(blobName);

                reportStream.Position = 0;
                blobClient.Upload(reportStream, overwrite: true);

                _logger.LogInformation("Report uploaded successfully to Azure (sync): {ReportName}", reportName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload report to Azure (sync): {ReportName}", reportName);
                return false;
            }
        }

        public Stream? DownloadReportSync(string reportName)
        {
            try
            {
                var blobName = GetBlobName(reportName);
                var blobClient = _containerClient.GetBlobClient(blobName);

                if (!blobClient.Exists())
                {
                    _logger.LogWarning("Report not found in Azure (sync): {ReportName}", reportName);
                    return null;
                }

                var memoryStream = new MemoryStream();
                blobClient.DownloadTo(memoryStream);
                memoryStream.Position = 0;

                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download report from Azure (sync): {ReportName}", reportName);
                return null;
            }
        }

        public List<string> ListReportsSync()
        {
            var reports = new List<string>();

            try
            {
                foreach (var blobItem in _containerClient.GetBlobs())
                {
                    var reportName = Path.GetFileNameWithoutExtension(blobItem.Name);
                    reports.Add(reportName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list reports from Azure (sync)");
            }

            return reports;
        }
    }
}

