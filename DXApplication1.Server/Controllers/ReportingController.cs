#nullable enable
using DevExpress.XtraReports.UI;
using DXApplication1.PredefinedReports;
using DXApplication1.Server.Models;
using DXApplication1.Services;
using ESS.Platform.Authorization.Attributes;
using ESS.Platform.Authorization.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DXApplication1.Server.Controllers
{
    /// <summary>
    /// Controller for reporting-related API endpoints.
    /// Provides report listing with metadata, report saving with Save/SaveAs logic,
    /// and report layout retrieval.
    /// </summary>
    [Route("api/v1/reporting")]
    [ApiController]
    public class ReportingController : ControllerBase
    {
        private readonly IAzureBlobStorageService _azureBlobStorageService;
        private readonly ILogger<ReportingController> _logger;

        // Regex pattern for sanitizing log values - removes newlines and control characters
        private static readonly Regex LogSanitizePattern = new Regex(@"[\r\n\t]+", RegexOptions.Compiled);

        public ReportingController(
            IAzureBlobStorageService azureBlobStorageService,
            ILogger<ReportingController> logger)
        {
            _azureBlobStorageService = azureBlobStorageService;
            _logger = logger;
        }

        /// <summary>
        /// Sanitizes a string for safe logging by removing newlines and control characters.
        /// This prevents log forging attacks.
        /// </summary>
        private static string SanitizeForLog(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return LogSanitizePattern.Replace(value, " ");
        }

        /// <summary>
        /// Gets a list of available reports with metadata, including the isPredefined flag.
        /// Predefined reports are built-in templates that cannot be overwritten.
        /// User reports are stored in Azure Blob Storage.
        /// </summary>
        [HttpGet("list-with-metadata")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public IActionResult GetReportsWithMetadata()
        {
            try
            {
                var reports = new List<ReportInfo>();

                // Add predefined reports (from ReportsFactory)
                foreach (var predefinedReport in ReportsFactory.Reports)
                {
                    reports.Add(new ReportInfo
                    {
                        Name = predefinedReport.Key,
                        DisplayName = predefinedReport.Key,
                        IsPredefined = true,
                        Description = $"Predefined report: {predefinedReport.Key}"
                    });
                }

                // Add user reports from Azure Blob Storage
                var userReports = _azureBlobStorageService.ListReportsSync();
                foreach (var userReportName in userReports)
                {
                    // Skip if a predefined report with the same name exists
                    if (ReportsFactory.Reports.ContainsKey(userReportName))
                    {
                        continue;
                    }

                    reports.Add(new ReportInfo
                    {
                        Name = userReportName,
                        DisplayName = userReportName,
                        IsPredefined = false,
                        Description = "User-created report"
                    });
                }

                return Ok(new ReportsListResponse { Reports = reports });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get reports list with metadata");
                return StatusCode(500, new { error = "Failed to retrieve reports list" });
            }
        }

        /// <summary>
        /// Gets a simple list of available report names (for backwards compatibility).
        /// </summary>
        [HttpGet("list")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public IActionResult GetReportsList()
        {
            try
            {
                var azureReports = _azureBlobStorageService.ListReportsSync();
                var predefinedReports = ReportsFactory.Reports.Keys;

                var allReports = azureReports
                    .Union(predefinedReports)
                    .Distinct()
                    .ToList();

                return Ok(allReports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get reports list");
                return StatusCode(500, new { error = "Failed to retrieve reports list" });
            }
        }

        /// <summary>
        /// Saves a report with Save/SaveAs logic.
        /// - For predefined reports: Only SaveAs is allowed (must provide newReportName)
        /// - For user reports: Both Save (overwrite) and SaveAs are allowed
        /// </summary>
        [HttpPost("save")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.Create)]
        public IActionResult SaveReport([FromBody] SaveReportRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (string.IsNullOrWhiteSpace(request.ReportData))
            {
                return BadRequest(new { error = "Report data is required" });
            }

            try
            {
                var originalName = request.ReportUrl;
                var isPredefined = ReportsFactory.Reports.ContainsKey(originalName);

                string targetReportName;

                if (request.SaveAs)
                {
                    // SaveAs operation - must provide a new name
                    if (string.IsNullOrWhiteSpace(request.NewReportName))
                    {
                        return BadRequest(new { error = "New report name is required for Save As operation" });
                    }

                    targetReportName = request.NewReportName.Trim();

                    // Validate the new name doesn't conflict with predefined reports
                    if (ReportsFactory.Reports.ContainsKey(targetReportName))
                    {
                        return BadRequest(new { error = $"Cannot save over predefined report '{targetReportName}'. Choose a different name." });
                    }
                }
                else
                {
                    // Save operation (overwrite)
                    if (isPredefined)
                    {
                        return BadRequest(new { error = $"Cannot overwrite predefined report '{originalName}'. Use Save As instead." });
                    }

                    targetReportName = originalName;
                }

                // Save the report to Azure Blob Storage
                using var reportStream = new MemoryStream(Encoding.UTF8.GetBytes(request.ReportData));
                var success = _azureBlobStorageService.UploadReportSync(targetReportName, reportStream);

                if (!success)
                {
                    return StatusCode(500, new { error = "Failed to save report to storage" });
                }

                _logger.LogInformation(
                    "Report saved successfully: {ReportName} (SaveAs: {SaveAs})",
                    SanitizeForLog(targetReportName),
                    request.SaveAs);

                return Ok(new SaveReportResponse
                {
                    ReportName = targetReportName,
                    Message = request.SaveAs
                        ? $"Report saved as '{targetReportName}' successfully"
                        : $"Report '{targetReportName}' saved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save report: {ReportUrl}", SanitizeForLog(request.ReportUrl));
                return StatusCode(500, new { error = "Failed to save report" });
            }
        }

        /// <summary>
        /// Gets the report layout data (XML) for a specific report.
        /// </summary>
        [HttpGet("layout")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public IActionResult GetReportLayout([FromQuery] string reportName)
        {
            if (string.IsNullOrWhiteSpace(reportName))
            {
                return BadRequest(new { error = "Report name is required" });
            }

            try
            {
                // Check if it's a predefined report
                if (ReportsFactory.Reports.TryGetValue(reportName, out var reportFactory))
                {
                    using var ms = new MemoryStream();
                    using XtraReport report = reportFactory();
                    report.SaveLayoutToXml(ms);
                    ms.Position = 0;
                    using var reader = new StreamReader(ms);
                    var layoutXml = reader.ReadToEnd();
                    return Content(layoutXml, "application/xml");
                }

                // Try to get from Azure Blob Storage
                using var stream = _azureBlobStorageService.DownloadReportSync(reportName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var layoutXml = reader.ReadToEnd();
                    return Content(layoutXml, "application/xml");
                }

                return NotFound(new { error = $"Report '{reportName}' not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get report layout: {ReportName}", SanitizeForLog(reportName));
                return StatusCode(500, new { error = "Failed to retrieve report layout" });
            }
        }
    }
}
