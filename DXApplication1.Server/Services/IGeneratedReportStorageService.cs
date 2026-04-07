#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DXApplication1.Services
{
    /// <summary>
    /// Metadata for a generated report stored in blob storage.
    /// </summary>
    public class GeneratedReportMetadata
    {
        /// <summary>
        /// Unique identifier for the generated report (blob name without extension).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Original report template name used to generate this report.
        /// </summary>
        public string ReportName { get; set; } = string.Empty;

        /// <summary>
        /// The learner external ID for whom the report was generated.
        /// </summary>
        public string LearnerExternalId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the learner (e.g., "Emma Thompson").
        /// </summary>
        public string LearnerName { get; set; } = string.Empty;

        /// <summary>
        /// Organization ID of the user who generated the report.
        /// </summary>
        public string OrgId { get; set; } = string.Empty;

        /// <summary>
        /// External ID of the user who generated the report.
        /// </summary>
        public string UserExternalId { get; set; } = string.Empty;

        /// <summary>
        /// File format (e.g., "pdf", "xlsx").
        /// </summary>
        public string Format { get; set; } = "pdf";

        /// <summary>
        /// When the report was generated (UTC).
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Size of the file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// The full blob name including path and extension.
        /// </summary>
        public string BlobName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for storing and retrieving generated per-pupil reports in Azure Blob Storage.
    /// Uses organization-based containers (one container per OrgId) with user-scoped blob paths.
    /// Storage structure: {containerPrefix}-{orgId} / {userExternalId}/{reportName}_{learnerExternalId}_{timestamp}.{format}
    /// </summary>
    public interface IGeneratedReportStorageService
    {
        /// <summary>
        /// Uploads a generated report to blob storage with metadata tags.
        /// </summary>
        /// <param name="reportStream">The report content stream.</param>
        /// <param name="reportName">Original report template name.</param>
        /// <param name="learnerExternalId">Learner's external ID.</param>
        /// <param name="learnerName">Learner's display name.</param>
        /// <param name="orgId">Organization ID of the generating user.</param>
        /// <param name="userExternalId">External ID of the generating user.</param>
        /// <param name="format">File format (pdf, xlsx).</param>
        /// <returns>Metadata of the stored report.</returns>
        Task<GeneratedReportMetadata> UploadGeneratedReportAsync(
            Stream reportStream,
            string reportName,
            string learnerExternalId,
            string learnerName,
            string orgId,
            string userExternalId,
            string format = "pdf");

        /// <summary>
        /// Lists generated reports for a specific user (by OrgId and UserExternalId).
        /// Uses prefix-based listing within the organization's container.
        /// </summary>
        /// <param name="orgId">Organization ID to filter by.</param>
        /// <param name="userExternalId">User's external ID to filter by.</param>
        /// <returns>List of generated report metadata.</returns>
        Task<List<GeneratedReportMetadata>> ListUserReportsAsync(string orgId, string userExternalId);

        /// <summary>
        /// Downloads a generated report by its ID.
        /// </summary>
        /// <param name="reportId">The unique report ID (blob name without extension).</param>
        /// <param name="orgId">Organization ID for access validation.</param>
        /// <param name="userExternalId">User's external ID for access validation.</param>
        /// <returns>The report stream and metadata, or null if not found or access denied.</returns>
        Task<(Stream? Stream, GeneratedReportMetadata? Metadata)> DownloadReportAsync(
            string reportId,
            string orgId,
            string userExternalId);

        /// <summary>
        /// Deletes a generated report by its ID.
        /// </summary>
        /// <param name="reportId">The unique report ID.</param>
        /// <param name="orgId">Organization ID for access validation.</param>
        /// <param name="userExternalId">User's external ID for access validation.</param>
        /// <returns>True if deleted successfully, false if not found or access denied.</returns>
        Task<bool> DeleteReportAsync(string reportId, string orgId, string userExternalId);
    }
}
