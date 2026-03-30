using DevExpress.XtraReports.UI;
using DXApplication1.PredefinedReports;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DXApplication1.Services
{
    public class CustomReportStorageWebExtension : DevExpress.XtraReports.Web.Extensions.ReportStorageWebExtension
    {
        private readonly IAzureBlobStorageService _azureBlobStorageService;
        private readonly ILogger<CustomReportStorageWebExtension> _logger;

        public CustomReportStorageWebExtension(
            IAzureBlobStorageService azureBlobStorageService, 
            ILogger<CustomReportStorageWebExtension> logger)
        {
            _azureBlobStorageService = azureBlobStorageService;
            _logger = logger;
        }

        public override bool CanSetData(string url) => true;

        public override bool IsValidUrl(string url) => !string.IsNullOrWhiteSpace(url);

        public override byte[] GetData(string url)
        {
            // Try to get from Azure Blob Storage (using synchronous method)
            var stream = _azureBlobStorageService.DownloadReportSync(url);
            if (stream != null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }

            // Fall back to predefined reports
            if (ReportsFactory.Reports.ContainsKey(url))
            {
                using var ms = new MemoryStream();
                using XtraReport report = ReportsFactory.Reports[url]();
                report.SaveLayoutToXml(ms);
                return ms.ToArray();
            }
            throw new DevExpress.XtraReports.Web.ClientControls.FaultException($"Could not find report '{url}'.");
        }

        public override Dictionary<string, string> GetUrls()
        {
            var azureReports = _azureBlobStorageService.ListReportsSync();

            return azureReports
                .Union(ReportsFactory.Reports.Select(x => x.Key))
                .ToDictionary(x => x, x => x);
        }

        public override void SetData(XtraReport report, string url)
        {
            using var azureStream = new MemoryStream();
            report.SaveLayoutToXml(azureStream);
            azureStream.Position = 0;
            _azureBlobStorageService.UploadReportSync(url, azureStream);
            _logger.LogInformation("Report saved to Azure Blob Storage: {ReportName}", url);
        }

        public override string SetNewData(XtraReport report, string defaultUrl)
        {
            SetData(report, defaultUrl);
            return defaultUrl;
        }
    }
}