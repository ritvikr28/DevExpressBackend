using DevExpress.DataAccess.DataFederation;
using DevExpress.DataAccess.Json;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.Parameters;
using System.Drawing;

namespace DXApplication1.PredefinedReports
{
    /// <summary>
    /// A report that demonstrates Data Federation with all 3 data sources (Pupil, Staff, Assessment)
    /// linked together via JOINs, with dynamic column selection from the UI via report parameters.
    /// 
    /// Data Federation joins:
    /// - Pupil (primary) LEFT JOIN Assessment ON PupilId
    /// - Pupil LEFT JOIN Staff (for demonstration - linked conceptually)
    /// 
    /// Parameters allow users to specify which columns to fetch from each source:
    /// - PupilColumns: Columns from Pupil data (e.g., "PupilId,FirstName,LastName")
    /// - StaffColumns: Columns from Staff data (e.g., "StaffId,FirstName,Role")
    /// - AssessmentColumns: Columns from Assessment data (e.g., "AssessmentId,Subject,Score")
    /// 
    /// IMPORTANT: For Data Federation to work:
    /// 1. CustomFederationDataSourceProviderFactory must be registered in Program.cs
    /// 2. JSON connections must be available through CustomApiDataConnectionStorage
    /// 3. The FederationDataSource references sources by their ConnectionName
    /// </summary>
    public partial class TestReport : XtraReport
    {
        public TestReport()
        {
            InitializeComponent();
            SetupFederatedDataSourceWithParameters();
        }

        /// <summary>
        /// Sets up report parameters for dynamic column selection and creates a 
        /// FederationDataSource that JOINs all 3 data sources (Pupil, Staff, Assessment).
        /// </summary>
        private void SetupFederatedDataSourceWithParameters()
        {
            // ======================================================================
            // REPORT PARAMETERS for dynamic column selection
            // These parameters allow the UI to specify which columns to fetch
            // ======================================================================
            
            var pupilColumnsParam = new Parameter
            {
                Name = "PupilColumns",
                Description = "Columns to fetch for Pupil data (comma-separated)",
                Type = typeof(string),
                Value = "PupilId,FirstName,LastName,DateOfBirth,Class",
                Visible = true
            };
            
            var staffColumnsParam = new Parameter
            {
                Name = "StaffColumns", 
                Description = "Columns to fetch for Staff data (comma-separated)",
                Type = typeof(string),
                Value = "StaffId,FirstName,LastName,Role,Department",
                Visible = true
            };
            
            var assessmentColumnsParam = new Parameter
            {
                Name = "AssessmentColumns",
                Description = "Columns to fetch for Assessment data (comma-separated)",
                Type = typeof(string),
                Value = "AssessmentId,PupilId,Subject,Score,Date",
                Visible = true
            };

            this.Parameters.AddRange(new Parameter[] { pupilColumnsParam, staffColumnsParam, assessmentColumnsParam });

            // ======================================================================
            // JSON DATA SOURCES - Using Dynamic connections with parameter-based columns
            // These use {?ParameterName} syntax in api-connections.json for column filtering
            // ======================================================================

            var pupilJsonSource = new JsonDataSource
            {
                Name = "PupilJsonSource",
                ConnectionName = "PupilDynamic"  // Uses {?PupilColumns} parameter
            };

            var staffJsonSource = new JsonDataSource
            {
                Name = "StaffJsonSource",
                ConnectionName = "StaffDynamic"  // Uses {?StaffColumns} parameter
            };

            var assessmentJsonSource = new JsonDataSource
            {
                Name = "AssessmentJsonSource",
                ConnectionName = "AssessmentDynamic"  // Uses {?AssessmentColumns} parameter
            };

            // Add JSON sources to component storage so they can be resolved
            this.ComponentStorage.Add(pupilJsonSource);
            this.ComponentStorage.Add(staffJsonSource);
            this.ComponentStorage.Add(assessmentJsonSource);

            // ======================================================================
            // FEDERATION DATA SOURCE - Links all 3 data sources via JOINs
            // ======================================================================

            var federationDataSource = new FederationDataSource();
            federationDataSource.Name = "AllSourcesFederation";

            // Create Source wrappers for Data Federation
            var pupilSource = new Source("Pupils", pupilJsonSource, "");
            var staffSource = new Source("Staff", staffJsonSource, "");
            var assessmentSource = new Source("Assessments", assessmentJsonSource, "");

            // Create SourceNode wrappers (required for SelectNode and JoinElement)
            var pupilSourceNode = new SourceNode(pupilSource, "Pupils");
            var staffSourceNode = new SourceNode(staffSource, "Staff");
            var assessmentSourceNode = new SourceNode(assessmentSource, "Assessments");

            // Create the federated query with Pupil as the primary source
            var federatedQuery = new SelectNode(pupilSourceNode)
            {
                Alias = "PupilStaffAssessments"
            };

            // JOIN Assessment on PupilId (Pupil.PupilId = Assessment.PupilId)
            federatedQuery.SubNodes.Add(new JoinElement(assessmentSourceNode, JoinType.LeftOuter,
                "[Pupils.PupilId] = [Assessments.PupilId]"));

            // CROSS JOIN Staff (since Staff doesn't have a direct FK relationship)
            // In a real scenario, you might have a relationship like ClassTeacher or Department
            // For this demo, we include Staff data as a separate section in the same federated query
            federatedQuery.SubNodes.Add(new JoinElement(staffSourceNode, JoinType.LeftOuter,
                "1 = 1"));  // Cross join - all staff visible

            // ======================================================================
            // SELECT COLUMNS from each source
            // These are the columns that will be available in the federated result
            // ======================================================================

            // Pupil columns
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "PupilId"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "FirstName") { Alias = "PupilFirstName" });
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "LastName") { Alias = "PupilLastName" });
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "DateOfBirth"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "Class"));

            // Assessment columns
            federatedQuery.Expressions.Add(new SelectColumnExpression(assessmentSourceNode, "AssessmentId"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(assessmentSourceNode, "Subject"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(assessmentSourceNode, "Score"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(assessmentSourceNode, "Date") { Alias = "AssessmentDate" });

            // Staff columns
            federatedQuery.Expressions.Add(new SelectColumnExpression(staffSourceNode, "StaffId"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(staffSourceNode, "FirstName") { Alias = "StaffFirstName" });
            federatedQuery.Expressions.Add(new SelectColumnExpression(staffSourceNode, "LastName") { Alias = "StaffLastName" });
            federatedQuery.Expressions.Add(new SelectColumnExpression(staffSourceNode, "Role"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(staffSourceNode, "Department"));

            // Add the query to the federation
            federationDataSource.Queries.Add(federatedQuery);

            // Fill the federation schema (required for designer)
            federationDataSource.RebuildResultSchema();

            // Add to component storage
            this.ComponentStorage.Add(federationDataSource);

            // Set as the report's data source
            this.DataSource = federationDataSource;
            this.DataMember = "PupilStaffAssessments";

            // Create report bands
            SetupReportBands();
        }

        /// <summary>
        /// Creates the visual layout showing federated data from all 3 sources in a single detail band.
        /// </summary>
        private void SetupReportBands()
        {
            // ======================================================================
            // REPORT HEADER
            // ======================================================================
            var reportHeader = new ReportHeaderBand();
            reportHeader.HeightF = 60F;
            reportHeader.Name = "ReportHeader";

            var titleLabel = new XRLabel();
            titleLabel.Text = "FEDERATED DATA REPORT (All 3 Sources Linked)";
            titleLabel.SizeF = new SizeF(750F, 35F);
            titleLabel.LocationF = new PointF(0F, 5F);
            titleLabel.Font = new Font("Arial", 16F, FontStyle.Bold);
            titleLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            reportHeader.Controls.Add(titleLabel);

            var instructionLabel = new XRLabel();
            instructionLabel.Text = "Set PupilColumns, StaffColumns, AssessmentColumns parameters to filter data";
            instructionLabel.SizeF = new SizeF(750F, 20F);
            instructionLabel.LocationF = new PointF(0F, 40F);
            instructionLabel.Font = new Font("Arial", 10F, FontStyle.Italic);
            instructionLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            reportHeader.Controls.Add(instructionLabel);
            this.Bands.Add(reportHeader);

            // ======================================================================
            // PAGE HEADER - Column Headers for all federated data
            // ======================================================================
            var pageHeader = new PageHeaderBand();
            pageHeader.HeightF = 50F;
            pageHeader.Name = "PageHeader";

            // Section labels
            var pupilSectionLabel = new XRLabel();
            pupilSectionLabel.Text = "PUPIL DATA";
            pupilSectionLabel.SizeF = new SizeF(250F, 20F);
            pupilSectionLabel.LocationF = new PointF(0F, 0F);
            pupilSectionLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            pupilSectionLabel.BackColor = Color.LightBlue;
            pupilSectionLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            pageHeader.Controls.Add(pupilSectionLabel);

            var assessmentSectionLabel = new XRLabel();
            assessmentSectionLabel.Text = "ASSESSMENT DATA";
            assessmentSectionLabel.SizeF = new SizeF(200F, 20F);
            assessmentSectionLabel.LocationF = new PointF(250F, 0F);
            assessmentSectionLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            assessmentSectionLabel.BackColor = Color.LightCoral;
            assessmentSectionLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            pageHeader.Controls.Add(assessmentSectionLabel);

            var staffSectionLabel = new XRLabel();
            staffSectionLabel.Text = "STAFF DATA";
            staffSectionLabel.SizeF = new SizeF(300F, 20F);
            staffSectionLabel.LocationF = new PointF(450F, 0F);
            staffSectionLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            staffSectionLabel.BackColor = Color.LightGreen;
            staffSectionLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            pageHeader.Controls.Add(staffSectionLabel);

            // Column headers table
            var headerTable = new XRTable();
            headerTable.LocationF = new PointF(0F, 22F);
            headerTable.SizeF = new SizeF(750F, 25F);
            headerTable.Name = "HeaderTable";

            var headerRow = new XRTableRow();
            headerRow.HeightF = 25F;

            // Headers for all federated columns
            var headers = new[] 
            { 
                // Pupil headers
                "Pupil ID", "First Name", "Last Name", "DOB", "Class",
                // Assessment headers
                "Subject", "Score", "Date",
                // Staff headers
                "Staff ID", "First Name", "Last Name", "Role", "Department"
            };

            var headerColors = new[]
            {
                // Pupil (light blue)
                Color.LightBlue, Color.LightBlue, Color.LightBlue, Color.LightBlue, Color.LightBlue,
                // Assessment (light coral)
                Color.LightCoral, Color.LightCoral, Color.LightCoral,
                // Staff (light green)
                Color.LightGreen, Color.LightGreen, Color.LightGreen, Color.LightGreen, Color.LightGreen
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = new XRTableCell
                {
                    Text = headers[i],
                    Font = new Font("Arial", 8F, FontStyle.Bold),
                    BackColor = headerColors[i],
                    Borders = DevExpress.XtraPrinting.BorderSide.All
                };
                headerRow.Cells.Add(cell);
            }

            headerTable.Rows.Add(headerRow);
            pageHeader.Controls.Add(headerTable);
            this.Bands.Add(pageHeader);

            // ======================================================================
            // DETAIL BAND - Shows joined data from all 3 sources in one row
            // ======================================================================
            var detailBand = new DetailBand();
            detailBand.HeightF = 25F;
            detailBand.Name = "Detail";

            var dataTable = new XRTable();
            dataTable.LocationF = new PointF(0F, 0F);
            dataTable.SizeF = new SizeF(750F, 25F);
            dataTable.Name = "DataTable";

            var dataRow = new XRTableRow();
            dataRow.HeightF = 25F;

            // Bindings for all federated columns (matching the headers)
            var bindings = new[] 
            { 
                // Pupil bindings
                "PupilId", "PupilFirstName", "PupilLastName", "DateOfBirth", "Class",
                // Assessment bindings
                "Subject", "Score", "AssessmentDate",
                // Staff bindings
                "StaffId", "StaffFirstName", "StaffLastName", "Role", "Department"
            };

            foreach (var binding in bindings)
            {
                var cell = new XRTableCell();
                cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"[{binding}]"));
                cell.Borders = DevExpress.XtraPrinting.BorderSide.All;
                cell.Font = new Font("Arial", 8F);
                dataRow.Cells.Add(cell);
            }

            dataTable.Rows.Add(dataRow);
            detailBand.Controls.Add(dataTable);
            this.Bands.Add(detailBand);

            // ======================================================================
            // PAGE FOOTER
            // ======================================================================
            var pageFooter = new PageFooterBand();
            pageFooter.HeightF = 30F;
            pageFooter.Name = "PageFooter";

            var pageInfo = new XRPageInfo();
            pageInfo.Format = "Page {0} of {1}";
            pageInfo.SizeF = new SizeF(200F, 20F);
            pageInfo.LocationF = new PointF(275F, 5F);
            pageInfo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            pageFooter.Controls.Add(pageInfo);
            this.Bands.Add(pageFooter);
        }
    }
}
