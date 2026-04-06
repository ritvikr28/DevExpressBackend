using DevExpress.DataAccess.Json;
using DevExpress.XtraReports.Parameters;
using DevExpress.XtraReports.UI;
using DXApplication1.PredefinedReports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace DXApplication1.Services
{
    public class CustomReportStorageWebExtension : DevExpress.XtraReports.Web.Extensions.ReportStorageWebExtension
    {
        private readonly IAzureBlobStorageService _azureBlobStorageService;
        private readonly ILogger<CustomReportStorageWebExtension> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _learnersEndpoint;

        public CustomReportStorageWebExtension(
            IAzureBlobStorageService azureBlobStorageService,
            ILogger<CustomReportStorageWebExtension> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _azureBlobStorageService = azureBlobStorageService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _learnersEndpoint = configuration["SimsApi:LearnersSearchEndpoint"]
                ?? "https://apisql-dev.learners.sims.co.uk/api/v6/data-export/learners-search";
        }

        public override bool CanSetData(string url) => true;

        public override bool IsValidUrl(string url) => !string.IsNullOrWhiteSpace(url);

        public override byte[] GetData(string url)
        {
            // URL format: "ReportName__id1,id2,id3" (viewer) or just "ReportName" (designer).
            string? rawIds = null;
            string reportName;
            const string sep = "__";
            var sepIndex = url.IndexOf(sep);
            if (sepIndex >= 0)
            {
                reportName = url.Substring(0, sepIndex);
                rawIds = Uri.UnescapeDataString(url.Substring(sepIndex + sep.Length));
            }
            else
            {
                reportName = url;
            }

            byte[] bytes = GetReportBytes(reportName);

            if (rawIds == null)
                return bytes;

            return InjectLiveData(bytes, rawIds).GetAwaiter().GetResult();
            // NOTE: GetData is a synchronous DevExpress override and cannot be made async.
            // .GetAwaiter().GetResult() is used intentionally here. This is safe as long as the
            // ASP.NET Core synchronisation context is not active in this code path (DevExpress
            // calls GetData from a thread-pool thread, not from the request thread directly).
        }

        private byte[] GetReportBytes(string reportName)
        {
            // Try to get from Azure Blob Storage
            var stream = _azureBlobStorageService.DownloadReportSync(reportName);
            if (stream != null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return EnsureIdsParameter(ms.ToArray());
            }

            // Fall back to predefined reports
            if (ReportsFactory.Reports.ContainsKey(reportName))
            {
                using var ms = new MemoryStream();
                using XtraReport report = ReportsFactory.Reports[reportName]();
                report.SaveLayoutToXml(ms);
                return EnsureIdsParameter(ms.ToArray());
            }

            throw new DevExpress.XtraReports.Web.ClientControls.FaultException(
                string.Format("Could not find report ''{0}''.", reportName));
        }

        private async Task<byte[]> InjectLiveData(byte[] bytes, string rawIds)
        {
            var json = await FetchApiJson(rawIds);

            using var report = new XtraReport();
            using var inMs = new MemoryStream(bytes);
            report.LoadLayoutFromXml(inMs);

            var jsonDs = FindJsonDataSource(report);
            if (jsonDs != null)
            {
                jsonDs.JsonSource = new CustomJsonSource(json);
                await jsonDs.FillAsync();
            }
            else
            {
                _logger.LogWarning("InjectLiveData: no JsonDataSource found in report");
            }

            // IDs already resolved via URL – hide parameter panel in the viewer.
            if (report.Parameters["userId"] is { } p)
                p.Visible = false;

            using var outMs = new MemoryStream();
            report.SaveLayoutToXml(outMs);
            return outMs.ToArray();
        }

        // rawIds is either:
        //   "b64_<base64url>" — stateless token encoded client-side (viewer, k8s-safe)
        //   "guid1,guid2"     — comma-separated typed in designer preview parameter panel
        private async Task<string> FetchApiJson(string rawIds)
        {
            string[] ids;
            const string b64Prefix = "b64_";
            if (rawIds.StartsWith(b64Prefix, StringComparison.Ordinal))
            {
                ids = DecodeBase64UrlIds(rawIds.Substring(b64Prefix.Length));
            }
            else
            {
                ids = rawIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim('"', '\'', ' '))
                    .Where(x => x.Length > 0)
                    .ToArray();
            }

            if (ids.Length == 0)
                return "[]";

            const int maxPageSize = 500;
            if (ids.Length > maxPageSize)
            {
                _logger.LogWarning("FetchApiJson: {Count} IDs exceed the maximum page size of {Max}; only the first {Max} will be fetched.", ids.Length, maxPageSize, maxPageSize);
                ids = ids.Take(maxPageSize).ToArray();
            }

            // Use the named client that automatically forwards the bearer token
            var client = _httpClientFactory.CreateClient("AuthenticatedApi");

            var body = new
            {
                pageNumber = 1,
                pageSize = 500,
                learnerExternalIds = ids,
                sortBy = "PreferredSurname",
                sortDirection = "ASC"
            };

            var response = await client.PostAsJsonAsync(_learnersEndpoint, body);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("FetchApiJson: error {StatusCode} - {Body}", (int)response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode();
            }

            var rawJson = await response.Content.ReadAsStringAsync();

            // Extract payload.learners and restructure into domain-grouped nested objects.
            var root = JsonSerializer.Deserialize<JsonElement>(rawJson);
            if (root.TryGetProperty("payload", out var payload) &&
                payload.TryGetProperty("learners", out var learners))
            {
                return TransformLearnersToNestedJson(learners);
            }

            return rawJson;
        }

        // Maps each flat SIMS learner to domain-grouped nested objects.
        // The nesting must match the sample JSON in ReportingControllers.cs exactly
        // so designer field paths resolve correctly at runtime.
        private static string TransformLearnersToNestedJson(JsonElement learnersArray)
        {
            var result = learnersArray.EnumerateArray().Select(l => new
            {
                identity = new
                {
                    learnerExternalId = Str(l, "learnerExternalId"),
                    preferredName     = Str(l, "preferredName"),
                    preferredForename = Str(l, "preferredForename"),
                    preferredSurname  = Str(l, "preferredSurname"),
                    legalName         = Str(l, "legalName"),
                    legalForename     = Str(l, "legalForename"),
                    legalSurname      = Str(l, "legalSurname")
                },
                personal = new
                {
                    dateOfBirth               = Str(l, "dateOfBirth"),
                    gender                    = Str(l, "gender"),
                    personalPronoun           = Str(l, "personalPronoun"),
                    englishAdditionalLanguage = Bool(l, "englishAdditionalLanguage"),
                    imagePath                 = Str(l, "imagePath")
                },
                enrollment = new
                {
                    admissionNumber  = Str(l, "admissionNumber"),
                    admissionDate    = Str(l, "admissionDate"),
                    leavingDate      = Str(l, "leavingDate"),
                    onRollState      = Str(l, "onRollState"),
                    enrollmentStatus = Str(l, "enrollmentStatus")
                }
            }).ToList();

            return JsonSerializer.Serialize(result);
        }

        private static string? Str(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString() : null;

        private static bool Bool(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

        // Decodes a base64url token produced by the client.
        // IDs are pipe-delimited UTF-8 text — works for integers, GUIDs, or any string ID.
        private static string[] DecodeBase64UrlIds(string token)
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            var pad = (4 - base64.Length % 4) % 4;
            base64 += new string('=', pad);
            var bytes = Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(bytes)
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim('"', '\'', ' '))
                .Where(id => id.Length > 0)
                .ToArray();
        }

        private static JsonDataSource? FindJsonDataSource(XtraReport report)
        {
            if (report.DataSource is JsonDataSource root) return root;
            foreach (Band band in report.Bands)
                foreach (XRControl control in band.Controls)
                    if (control is XtraReport sub && sub.DataSource is JsonDataSource subDs)
                        return subDs;
            return null;
        }

        private static byte[] EnsureIdsParameter(byte[] bytes)
        {
            using var report = new XtraReport();
            using var inMs = new MemoryStream(bytes);
            report.LoadLayoutFromXml(inMs);

            if (report.Parameters["userId"] != null)
                return bytes;

            report.Parameters.Add(new Parameter
            {
                Name = "userId",
                Description = "ID(s) - comma-separated for multiple (e.g. 1,2,3)",
                Type = typeof(string),
                Visible = true
            });

            using var outMs = new MemoryStream();
            report.SaveLayoutToXml(outMs);
            return outMs.ToArray();
        }

        public override async Task AfterGetDataAsync(string url, XtraReport report)
        {
            // When url is non-empty, live data was already injected in GetData – skip to avoid duplicate injection.
            if (!string.IsNullOrEmpty(url))
                return;

            var rawIds = report.Parameters["userId"]?.Value?.ToString();
            if (string.IsNullOrEmpty(rawIds))
                return;

            var jsonDs = FindJsonDataSource(report);
            if (jsonDs == null)
            {
                _logger.LogWarning("AfterGetDataAsync: no JsonDataSource found in report");
                return;
            }

            var json = await FetchApiJson(rawIds);
            jsonDs.JsonSource = new CustomJsonSource(json);
            await jsonDs.FillAsync();
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