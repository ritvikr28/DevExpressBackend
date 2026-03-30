using DevExpress.DataAccess.DataFederation;
using DevExpress.DataAccess.Json;
using DevExpress.XtraReports.UI;
using System.Drawing;

namespace DXApplication1.PredefinedReports
{
    /// <summary>
    /// A report that demonstrates Data Federation with JOIN operations.
    /// This report joins Pupil and Assessment data sources on PupilId,
    /// allowing fields from both sources to be used in a single detail band.
    /// 
    /// IMPORTANT: For Data Federation to work:
    /// 1. CustomFederationDataSourceProviderFactory must be registered in Program.cs
    /// 2. JSON connections must be available through CustomApiDataConnectionStorage
    /// 3. The FederationDataSource must reference sources by their ConnectionName
    /// </summary>
    public partial class FederatedReport : XtraReport
    {
        public FederatedReport()
        {
            InitializeComponent();
            SetupFederatedDataSource();
        }

        /// <summary>
        /// Creates a FederationDataSource that JOINs Pupil and Assessment data on PupilId.
        /// This enables displaying pupil information alongside their assessments in a single row.
        /// </summary>
        private void SetupFederatedDataSource()
        {
            // Create JSON data sources that reference connections from api-connections.json
            // These use StoreConnectionNameOnly so DevExpress resolves them at runtime
            var pupilJsonSource = new JsonDataSource
            {
                Name = "PupilJsonSource",
                ConnectionName = "Pupil"
            };

            var assessmentJsonSource = new JsonDataSource
            {
                Name = "AssessmentJsonSource", 
                ConnectionName = "Assessment"
            };

            // Add JSON sources to component storage so they can be resolved
            this.ComponentStorage.Add(pupilJsonSource);
            this.ComponentStorage.Add(assessmentJsonSource);

            // Create the Federation Data Source
            var federationDataSource = new FederationDataSource();
            federationDataSource.Name = "PupilAssessmentFederation";

            // Create Source wrappers for Data Federation
            // The third parameter is the data member/table name (empty string for root JSON arrays)
            var pupilSource = new Source("Pupils", pupilJsonSource, "");
            var assessmentSource = new Source("Assessments", assessmentJsonSource, "");

            // Create SourceNode wrappers (required for SelectNode and JoinElement)
            var pupilSourceNode = new SourceNode(pupilSource, "Pupils");
            var assessmentSourceNode = new SourceNode(assessmentSource, "Assessments");

            // Create the federated query with the primary source
            var federatedQuery = new SelectNode(pupilSourceNode)
            {
                Alias = "PupilWithAssessments"
            };

            // Add LEFT OUTER JOIN to Assessment source on PupilId
            // The condition uses the SourceNode aliases
            federatedQuery.SubNodes.Add(new JoinElement(assessmentSourceNode, JoinType.LeftOuter,
                "[Pupils.PupilId] = [Assessments.PupilId]"));

            // Select columns from Pupil source (using SourceNode)
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "PupilId"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "FirstName"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "LastName"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(pupilSourceNode, "Class"));

            // Select columns from Assessment source (using SourceNode)
            federatedQuery.Expressions.Add(new SelectColumnExpression(assessmentSourceNode, "Subject"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(assessmentSourceNode, "Score"));
            federatedQuery.Expressions.Add(new SelectColumnExpression(assessmentSourceNode, "Date") { Alias = "AssessmentDate" });

            // Add the query to the federation
            federationDataSource.Queries.Add(federatedQuery);

            // Fill the federation schema (required for designer)
            federationDataSource.RebuildResultSchema();

            // Add to component storage
            this.ComponentStorage.Add(federationDataSource);

            // Set as the report's data source
            this.DataSource = federationDataSource;
            this.DataMember = "PupilWithAssessments";

            // Create report bands
            SetupReportBands();
        }

        private void SetupReportBands()
        {
            // ======================================================================
            // REPORT HEADER
            // ======================================================================
            var reportHeader = new ReportHeaderBand();
            reportHeader.HeightF = 50F;
            reportHeader.Name = "ReportHeader";

            var titleLabel = new XRLabel();
            titleLabel.Text = "PUPIL ASSESSMENTS REPORT (Federated Data)";
            titleLabel.SizeF = new SizeF(700F, 35F);
            titleLabel.LocationF = new PointF(0F, 5F);
            titleLabel.Font = new Font("Arial", 16F, FontStyle.Bold);
            titleLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            reportHeader.Controls.Add(titleLabel);
            this.Bands.Add(reportHeader);

            // ======================================================================
            // PAGE HEADER (Column Headers)
            // ======================================================================
            var pageHeader = new PageHeaderBand();
            pageHeader.HeightF = 30F;
            pageHeader.Name = "PageHeader";

            var headerTable = new XRTable();
            headerTable.LocationF = new PointF(0F, 0F);
            headerTable.SizeF = new SizeF(700F, 25F);
            headerTable.Name = "HeaderTable";

            var headerRow = new XRTableRow();
            headerRow.HeightF = 25F;

            var headers = new[] { "Pupil ID", "First Name", "Last Name", "Class", "Subject", "Score", "Date" };
            foreach (var header in headers)
            {
                var cell = new XRTableCell
                {
                    Text = header,
                    Font = new Font("Arial", 10F, FontStyle.Bold),
                    BackColor = Color.LightBlue,
                    Borders = DevExpress.XtraPrinting.BorderSide.All
                };
                headerRow.Cells.Add(cell);
            }

            headerTable.Rows.Add(headerRow);
            pageHeader.Controls.Add(headerTable);
            this.Bands.Add(pageHeader);

            // ======================================================================
            // DETAIL BAND - Shows joined data from both Pupil and Assessment
            // ======================================================================
            var detailBand = new DetailBand();
            detailBand.HeightF = 25F;
            detailBand.Name = "Detail";

            var dataTable = new XRTable();
            dataTable.LocationF = new PointF(0F, 0F);
            dataTable.SizeF = new SizeF(700F, 25F);
            dataTable.Name = "DataTable";

            var dataRow = new XRTableRow();
            dataRow.HeightF = 25F;

            // Bind columns from the federated query result
            var bindings = new[] { "PupilId", "FirstName", "LastName", "Class", "Subject", "Score", "AssessmentDate" };
            foreach (var binding in bindings)
            {
                var cell = new XRTableCell();
                cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"[{binding}]"));
                cell.Borders = DevExpress.XtraPrinting.BorderSide.All;
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
            pageInfo.LocationF = new PointF(250F, 5F);
            pageInfo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            pageFooter.Controls.Add(pageInfo);
            this.Bands.Add(pageFooter);
        }
    }
}
