#nullable enable
using System.Text.Json.Serialization;

namespace DXApplication1.Server.Models
{
    /// <summary>
    /// Report information including metadata about whether it's predefined
    /// </summary>
    public class ReportInfo
    {
        /// <summary>
        /// Report identifier/name used by DevExpress
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name shown to users
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Whether this is a predefined report from the backend (cannot be overwritten)
        /// </summary>
        [JsonPropertyName("isPredefined")]
        public bool IsPredefined { get; set; }

        /// <summary>
        /// Optional description of the report
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// When the report was last modified (ISO 8601 format)
        /// </summary>
        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }
    }

    /// <summary>
    /// Response from the reports list API
    /// </summary>
    public class ReportsListResponse
    {
        [JsonPropertyName("reports")]
        public List<ReportInfo> Reports { get; set; } = new();
    }

    /// <summary>
    /// Save report request payload
    /// </summary>
    public class SaveReportRequest
    {
        /// <summary>
        /// The original report name (for Save operation)
        /// </summary>
        [JsonPropertyName("reportUrl")]
        public string ReportUrl { get; set; } = string.Empty;

        /// <summary>
        /// The new report name (for Save As operation)
        /// </summary>
        [JsonPropertyName("newReportName")]
        public string? NewReportName { get; set; }

        /// <summary>
        /// The report layout data (XML)
        /// </summary>
        [JsonPropertyName("reportData")]
        public string ReportData { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is a Save As operation
        /// </summary>
        [JsonPropertyName("saveAs")]
        public bool SaveAs { get; set; }
    }

    /// <summary>
    /// Save report response
    /// </summary>
    public class SaveReportResponse
    {
        /// <summary>
        /// The saved report name
        /// </summary>
        [JsonPropertyName("reportName")]
        public string ReportName { get; set; } = string.Empty;

        /// <summary>
        /// Success message
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
