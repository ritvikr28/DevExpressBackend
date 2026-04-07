#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DXApplication1.Server.Models
{
    /// <summary>
    /// Information about a generated report.
    /// </summary>
    public class GeneratedReportInfo
    {
        /// <summary>
        /// Unique identifier for the generated report.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Original report template name.
        /// </summary>
        [JsonPropertyName("reportName")]
        public string ReportName { get; set; } = string.Empty;

        /// <summary>
        /// Learner external ID for whom the report was generated.
        /// </summary>
        [JsonPropertyName("learnerExternalId")]
        public string LearnerExternalId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the learner.
        /// </summary>
        [JsonPropertyName("learnerName")]
        public string LearnerName { get; set; } = string.Empty;

        /// <summary>
        /// File format (e.g., "pdf", "xlsx").
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = "pdf";

        /// <summary>
        /// When the report was generated (ISO 8601).
        /// </summary>
        [JsonPropertyName("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Size of the file in bytes.
        /// </summary>
        [JsonPropertyName("fileSizeBytes")]
        public long FileSizeBytes { get; set; }
    }

    /// <summary>
    /// Request to generate per-pupil reports.
    /// </summary>
    public class GeneratePerPupilRequest
    {
        /// <summary>
        /// Name of the report template to use.
        /// </summary>
        [JsonPropertyName("reportName")]
        public string ReportName { get; set; } = string.Empty;

        /// <summary>
        /// List of learners to generate reports for.
        /// </summary>
        [JsonPropertyName("learners")]
        public List<LearnerForReport> Learners { get; set; } = new();

        /// <summary>
        /// Output format: "pdf" or "xlsx".
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = "pdf";
    }

    /// <summary>
    /// Learner information for report generation.
    /// </summary>
    public class LearnerForReport
    {
        /// <summary>
        /// Learner's external ID.
        /// </summary>
        [JsonPropertyName("learnerExternalId")]
        public string LearnerExternalId { get; set; } = string.Empty;

        /// <summary>
        /// Learner's display name.
        /// </summary>
        [JsonPropertyName("learnerName")]
        public string LearnerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from generate per-pupil endpoint.
    /// </summary>
    public class GeneratePerPupilResponse
    {
        /// <summary>
        /// List of successfully generated reports.
        /// </summary>
        [JsonPropertyName("generatedReports")]
        public List<GeneratedReportInfo> GeneratedReports { get; set; } = new();

        /// <summary>
        /// List of errors for any failed generations.
        /// </summary>
        [JsonPropertyName("errors")]
        public List<GenerationError> Errors { get; set; } = new();

        /// <summary>
        /// Total number of reports requested.
        /// </summary>
        [JsonPropertyName("totalRequested")]
        public int TotalRequested { get; set; }

        /// <summary>
        /// Number of reports successfully generated.
        /// </summary>
        [JsonPropertyName("totalGenerated")]
        public int TotalGenerated { get; set; }
    }

    /// <summary>
    /// Error information for a failed report generation.
    /// </summary>
    public class GenerationError
    {
        /// <summary>
        /// Learner external ID that failed.
        /// </summary>
        [JsonPropertyName("learnerExternalId")]
        public string LearnerExternalId { get; set; } = string.Empty;

        /// <summary>
        /// Learner name that failed.
        /// </summary>
        [JsonPropertyName("learnerName")]
        public string LearnerName { get; set; } = string.Empty;

        /// <summary>
        /// Error message.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from my-reports endpoint.
    /// </summary>
    public class MyReportsResponse
    {
        /// <summary>
        /// List of user's generated reports.
        /// </summary>
        [JsonPropertyName("reports")]
        public List<GeneratedReportInfo> Reports { get; set; } = new();
    }

    /// <summary>
    /// Request to download a generated report.
    /// </summary>
    public class DownloadReportRequest
    {
        /// <summary>
        /// The unique report ID to download.
        /// </summary>
        [JsonPropertyName("reportId")]
        public string ReportId { get; set; } = string.Empty;
    }
}
