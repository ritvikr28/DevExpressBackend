#nullable enable
using DevExpress.DataAccess.Json;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using DXApplication1.PredefinedReports;
using DXApplication1.Server.Models;
using DXApplication1.Services;
using ESS.Platform.Authorization.Attributes;
using ESS.Platform.Authorization.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DXApplication1.Server.Controllers
{
    /// <summary>
    /// Controller for per-pupil report generation and retrieval.
    /// Generates individual PDF/Excel reports per learner and stores them in Azure Blob Storage.
    /// </summary>
    [Route("api/v1/reporting/generated")]
    [ApiController]
    public class GeneratedReportsController : ControllerBase
    {
        private readonly IGeneratedReportStorageService _generatedReportStorageService;
        private readonly IAzureBlobStorageService _azureBlobStorageService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeneratedReportsController> _logger;
        private readonly string _learnersEndpoint;

        // Regex pattern for sanitizing log values - removes newlines and control characters
        private static readonly Regex LogSanitizePattern = new Regex(@"[\r\n\t]+", RegexOptions.Compiled);

        // JWT claim types - these may vary by identity provider
        private const string OrgIdClaimType = "org_id";
        private const string ExternalIdClaimType = "external_id";
        private const string SubjectClaimType = "sub";
        private const string NameClaimType = "name";

        public GeneratedReportsController(
            IGeneratedReportStorageService generatedReportStorageService,
            IAzureBlobStorageService azureBlobStorageService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GeneratedReportsController> logger)
        {
            _generatedReportStorageService = generatedReportStorageService;
            _azureBlobStorageService = azureBlobStorageService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _learnersEndpoint = configuration["SimsApi:LearnersSearchEndpoint"]
                ?? "https://apisql-dev.learners.sims.co.uk/api/v6/data-export/learners-search";
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
        /// Extracts OrgId from JWT claims.
        /// </summary>
        private string? GetOrgIdFromClaims()
        {
            var claim = User.FindFirst(OrgIdClaimType)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/org_id");
            return claim?.Value;
        }

        /// <summary>
        /// Extracts UserExternalId from JWT claims.
        /// Falls back to 'sub' claim if external_id is not present.
        /// </summary>
        private string? GetUserExternalIdFromClaims()
        {
            var claim = User.FindFirst(ExternalIdClaimType)
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/external_id")
                ?? User.FindFirst(SubjectClaimType)
                ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return claim?.Value;
        }

        /// <summary>
        /// Generates per-pupil reports for the specified learners.
        /// Each learner gets their own individual PDF/Excel file stored in Azure Blob Storage.
        /// </summary>
        [HttpPost("generate-per-pupil")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.Create)]
        public async Task<IActionResult> GeneratePerPupilReports([FromBody] GeneratePerPupilRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (string.IsNullOrWhiteSpace(request.ReportName))
            {
                return BadRequest(new { error = "Report name is required" });
            }

            if (request.Learners == null || request.Learners.Count == 0)
            {
                return BadRequest(new { error = "At least one learner is required" });
            }

            // Validate format
            var format = request.Format?.ToLowerInvariant() ?? "pdf";
            if (format != "pdf" && format != "xlsx")
            {
                return BadRequest(new { error = "Format must be 'pdf' or 'xlsx'" });
            }

            // Extract user identity from JWT claims
            var orgId = GetOrgIdFromClaims();
            var userExternalId = GetUserExternalIdFromClaims();

            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(userExternalId))
            {
                _logger.LogWarning("Missing OrgId or UserExternalId in JWT claims");
                return Unauthorized(new { error = "Unable to identify user from token" });
            }

            var response = new GeneratePerPupilResponse
            {
                TotalRequested = request.Learners.Count
            };

            try
            {
                // Load the report template
                byte[] reportTemplateBytes = GetReportTemplateBytes(request.ReportName);

                // Fetch learner data from API
                var learnerIds = request.Learners.Select(l => l.LearnerExternalId).ToArray();
                var learnerData = await FetchLearnerDataAsync(learnerIds);

                // Generate report for each learner
                foreach (var learner in request.Learners)
                {
                    try
                    {
                        // Find learner data from API response
                        var learnerJson = FindLearnerData(learnerData, learner.LearnerExternalId);
                        
                        if (learnerJson == null)
                        {
                            response.Errors.Add(new GenerationError
                            {
                                LearnerExternalId = learner.LearnerExternalId,
                                LearnerName = learner.LearnerName,
                                Error = "Learner data not found"
                            });
                            continue;
                        }

                        // Generate report for this single learner
                        using var reportStream = await GenerateReportForLearnerAsync(
                            reportTemplateBytes,
                            learnerJson,
                            format);

                        // Store the generated report
                        var metadata = await _generatedReportStorageService.UploadGeneratedReportAsync(
                            reportStream,
                            request.ReportName,
                            learner.LearnerExternalId,
                            learner.LearnerName,
                            orgId,
                            userExternalId,
                            format);

                        response.GeneratedReports.Add(new GeneratedReportInfo
                        {
                            Id = metadata.Id,
                            ReportName = metadata.ReportName,
                            LearnerExternalId = metadata.LearnerExternalId,
                            LearnerName = learner.LearnerName,
                            Format = metadata.Format,
                            GeneratedAt = metadata.GeneratedAt,
                            FileSizeBytes = metadata.FileSizeBytes
                        });

                        response.TotalGenerated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to generate report for learner {LearnerExternalId}",
                            SanitizeForLog(learner.LearnerExternalId));

                        response.Errors.Add(new GenerationError
                        {
                            LearnerExternalId = learner.LearnerExternalId,
                            LearnerName = learner.LearnerName,
                            Error = "Failed to generate report"
                        });
                    }
                }

                _logger.LogInformation(
                    "Generated {Count}/{Total} per-pupil reports for user {UserExternalId}",
                    response.TotalGenerated,
                    response.TotalRequested,
                    SanitizeForLog(userExternalId));

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate per-pupil reports: {ReportName}",
                    SanitizeForLog(request.ReportName));
                return StatusCode(500, new { error = "Failed to generate reports" });
            }
        }

        /// <summary>
        /// Gets the list of generated reports for the current user.
        /// </summary>
        [HttpGet("my-reports")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public async Task<IActionResult> GetMyReports()
        {
            var orgId = GetOrgIdFromClaims();
            var userExternalId = GetUserExternalIdFromClaims();

            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(userExternalId))
            {
                _logger.LogWarning("Missing OrgId or UserExternalId in JWT claims");
                return Unauthorized(new { error = "Unable to identify user from token" });
            }

            try
            {
                var reports = await _generatedReportStorageService.ListUserReportsAsync(orgId, userExternalId);

                var response = new MyReportsResponse
                {
                    Reports = reports.Select(m => new GeneratedReportInfo
                    {
                        Id = m.Id,
                        ReportName = m.ReportName,
                        LearnerExternalId = m.LearnerExternalId,
                        LearnerName = m.LearnerName,
                        Format = m.Format,
                        GeneratedAt = m.GeneratedAt,
                        FileSizeBytes = m.FileSizeBytes
                    }).OrderByDescending(r => r.GeneratedAt).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user's generated reports");
                return StatusCode(500, new { error = "Failed to retrieve reports" });
            }
        }

        /// <summary>
        /// Downloads a specific generated report.
        /// </summary>
        [HttpGet("download")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public async Task<IActionResult> DownloadReport([FromQuery] string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
            {
                return BadRequest(new { error = "Report ID is required" });
            }

            var orgId = GetOrgIdFromClaims();
            var userExternalId = GetUserExternalIdFromClaims();

            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(userExternalId))
            {
                _logger.LogWarning("Missing OrgId or UserExternalId in JWT claims");
                return Unauthorized(new { error = "Unable to identify user from token" });
            }

            try
            {
                var (stream, metadata) = await _generatedReportStorageService.DownloadReportAsync(
                    reportId, orgId, userExternalId);

                if (stream == null || metadata == null)
                {
                    return NotFound(new { error = "Report not found or access denied" });
                }

                var contentType = metadata.Format.ToLowerInvariant() switch
                {
                    "pdf" => "application/pdf",
                    "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    _ => "application/octet-stream"
                };

                var fileName = $"{metadata.ReportName}_{metadata.LearnerExternalId}.{metadata.Format}";

                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download report: {ReportId}", SanitizeForLog(reportId));
                return StatusCode(500, new { error = "Failed to download report" });
            }
        }

        /// <summary>
        /// Deletes a specific generated report.
        /// </summary>
        [HttpDelete("delete")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.Delete)]
        public async Task<IActionResult> DeleteReport([FromQuery] string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
            {
                return BadRequest(new { error = "Report ID is required" });
            }

            var orgId = GetOrgIdFromClaims();
            var userExternalId = GetUserExternalIdFromClaims();

            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(userExternalId))
            {
                _logger.LogWarning("Missing OrgId or UserExternalId in JWT claims");
                return Unauthorized(new { error = "Unable to identify user from token" });
            }

            try
            {
                var deleted = await _generatedReportStorageService.DeleteReportAsync(
                    reportId, orgId, userExternalId);

                if (!deleted)
                {
                    return NotFound(new { error = "Report not found or access denied" });
                }

                return Ok(new { message = "Report deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete report: {ReportId}", SanitizeForLog(reportId));
                return StatusCode(500, new { error = "Failed to delete report" });
            }
        }

        /// <summary>
        /// Gets the report template bytes from predefined reports or blob storage.
        /// </summary>
        private byte[] GetReportTemplateBytes(string reportName)
        {
            // Try to get from Azure Blob Storage first
            var stream = _azureBlobStorageService.DownloadReportSync(reportName);
            if (stream != null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }

            // Fall back to predefined reports
            if (ReportsFactory.Reports.TryGetValue(reportName, out var reportFactory))
            {
                using var ms = new MemoryStream();
                using XtraReport report = reportFactory();
                report.SaveLayoutToXml(ms);
                return ms.ToArray();
            }

            throw new InvalidOperationException($"Report template '{reportName}' not found");
        }

        /// <summary>
        /// Fetches learner data from the API.
        /// </summary>
        private async Task<string> FetchLearnerDataAsync(string[] learnerIds)
        {
            if (learnerIds.Length == 0)
                return "[]";

            var client = _httpClientFactory.CreateClient("AuthenticatedApi");

            var body = new
            {
                pageNumber = 1,
                pageSize = 500,
                learnerExternalIds = learnerIds,
                sortBy = "PreferredSurname",
                sortDirection = "ASC"
            };

            var response = await client.PostAsJsonAsync(_learnersEndpoint, body);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("FetchLearnerData: error {StatusCode} - {Body}",
                    (int)response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Finds a specific learner's data from the API response.
        /// </summary>
        private string? FindLearnerData(string apiResponse, string learnerExternalId)
        {
            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(apiResponse);
                if (!root.TryGetProperty("payload", out var payload) ||
                    !payload.TryGetProperty("learners", out var learners))
                {
                    _logger.LogWarning(
                        "FindLearnerData: API response missing payload.learners structure for learner {LearnerExternalId}",
                        SanitizeForLog(learnerExternalId));
                    return null;
                }

                foreach (var learner in learners.EnumerateArray())
                {
                    if (learner.TryGetProperty("learnerExternalId", out var idProp) &&
                        idProp.GetString()?.Equals(learnerExternalId, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // Transform to the nested structure expected by reports
                        var transformed = TransformLearnerToNestedJson(learner);
                        return $"[{transformed}]";
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "FindLearnerData: Failed to parse API response JSON for learner {LearnerExternalId}",
                    SanitizeForLog(learnerExternalId));
            }

            return null;
        }

        /// <summary>
        /// Transforms a flat learner JSON to the nested structure expected by reports.
        /// </summary>
        private static string TransformLearnerToNestedJson(JsonElement learner)
        {
            var result = new
            {
                identity = new
                {
                    learnerExternalId = Str(learner, "learnerExternalId"),
                    preferredName = Str(learner, "preferredName"),
                    preferredForename = Str(learner, "preferredForename"),
                    preferredSurname = Str(learner, "preferredSurname"),
                    legalName = Str(learner, "legalName"),
                    legalForename = Str(learner, "legalForename"),
                    legalSurname = Str(learner, "legalSurname")
                },
                personal = new
                {
                    dateOfBirth = Str(learner, "dateOfBirth"),
                    gender = Str(learner, "gender"),
                    personalPronoun = Str(learner, "personalPronoun"),
                    englishAdditionalLanguage = Bool(learner, "englishAdditionalLanguage"),
                    imagePath = Str(learner, "imagePath")
                },
                enrollment = new
                {
                    admissionNumber = Str(learner, "admissionNumber"),
                    admissionDate = Str(learner, "admissionDate"),
                    leavingDate = Str(learner, "leavingDate"),
                    onRollState = Str(learner, "onRollState"),
                    enrollmentStatus = Str(learner, "enrollmentStatus")
                }
            };

            return JsonSerializer.Serialize(result);
        }

        private static string? Str(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString() : null;

        private static bool Bool(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

        /// <summary>
        /// Generates a report for a single learner and returns the output stream.
        /// </summary>
        private async Task<Stream> GenerateReportForLearnerAsync(byte[] templateBytes, string learnerJson, string format)
        {
            using var report = new XtraReport();
            using var templateStream = new MemoryStream(templateBytes);
            report.LoadLayoutFromXml(templateStream);

            // Inject the learner data
            var jsonDs = FindJsonDataSource(report);
            if (jsonDs != null)
            {
                jsonDs.JsonSource = new CustomJsonSource(learnerJson);
                await jsonDs.FillAsync();
            }

            // Hide the userId parameter if present
            if (report.Parameters["userId"] is { } p)
                p.Visible = false;

            // Export to the requested format
            var outputStream = new MemoryStream();

            if (format == "pdf")
            {
                await report.ExportToPdfAsync(outputStream);
            }
            else if (format == "xlsx")
            {
                await report.ExportToXlsxAsync(outputStream);
            }

            outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Finds the JsonDataSource in a report.
        /// </summary>
        private static JsonDataSource? FindJsonDataSource(XtraReport report)
        {
            if (report.DataSource is JsonDataSource root) return root;
            foreach (Band band in report.Bands)
                foreach (XRControl control in band.Controls)
                    if (control is XtraReport sub && sub.DataSource is JsonDataSource subDs)
                        return subDs;
            return null;
        }
    }
}
