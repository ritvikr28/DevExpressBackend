using DevExpress.DataAccess.Json;
using DevExpress.XtraReports.UI;
using System.Drawing;

namespace DXApplication1.PredefinedReports
{
    /// <summary>
    /// A report that demonstrates multiple JSON data sources.
    /// Each data source is configured to use the StoreConnectionNameOnly pattern,
    /// which means DevExpress will resolve the actual connection from the registered
    /// IDataSourceWizardJsonConnectionStorage (CustomApiDataConnectionStorage).
    /// When previewed, DevExpress will call each endpoint separately to fetch data.
    /// 
    /// This report uses:
    /// - Primary Detail band for Pupil data (bound to report's DataSource)
    /// - DetailReportBand for Staff data (bound to staffDataSource)
    /// - DetailReportBand for Assessment data (bound to assessmentDataSource)
    /// </summary>
    public partial class MultiSourceReport : XtraReport
    {
        public MultiSourceReport()
        {
            InitializeComponent();
            SetupMultipleDataSources();
        }

        /// <summary>
        /// Configures multiple JSON data sources for this report.
        /// Each JsonDataSource uses StoreConnectionNameOnly = true so that DevExpress
        /// resolves the actual URI from the registered connection storage.
        /// </summary>
        private void SetupMultipleDataSources()
        {
            // Create Pupil data source - uses connection name, resolved at runtime
            var pupilDataSource = new JsonDataSource
            {
                Name = "PupilDataSource",
                ConnectionName = "Pupil" // Matches the Name in api-connections.json
            };

            // Create Staff data source
            var staffDataSource = new JsonDataSource
            {
                Name = "StaffDataSource",
                ConnectionName = "Staff" // Matches the Name in api-connections.json
            };

            // Create Assessment data source
            var assessmentDataSource = new JsonDataSource
            {
                Name = "AssessmentDataSource",
                ConnectionName = "Assessment" // Matches the Name in api-connections.json
            };

            // Add all data sources to the report's component container
            this.ComponentStorage.Add(pupilDataSource);
            this.ComponentStorage.Add(staffDataSource);
            this.ComponentStorage.Add(assessmentDataSource);

            // Set Pupil as the primary data source (for the main Detail band)
            this.DataSource = pupilDataSource;
            this.DataMember = string.Empty;

            // ======================================================================
            // STAFF SECTION - Using DetailReportBand with its own DataSource
            // ======================================================================
            var staffDetailReport = new DetailReportBand();
            staffDetailReport.Name = "StaffDetailReport";
            staffDetailReport.DataSource = staffDataSource;
            staffDetailReport.DataMember = string.Empty;
            staffDetailReport.Level = 0;

            // Staff section header
            var staffHeader = new GroupHeaderBand();
            staffHeader.HeightF = 50F;
            staffHeader.Name = "StaffGroupHeader";
            staffHeader.RepeatEveryPage = true;

            var staffSectionLabel = new XRLabel();
            staffSectionLabel.Text = "STAFF";
            staffSectionLabel.SizeF = new SizeF(650F, 25F);
            staffSectionLabel.LocationF = new PointF(0F, 0F);
            staffSectionLabel.Font = new Font("Arial", 14F, FontStyle.Bold);
            staffSectionLabel.BackColor = Color.LightGreen;
            staffHeader.Controls.Add(staffSectionLabel);

            // Staff table header
            var staffHeaderTable = new XRTable();
            staffHeaderTable.LocationF = new PointF(0F, 25F);
            staffHeaderTable.SizeF = new SizeF(650F, 25F);
            staffHeaderTable.Name = "StaffHeaderTable";

            var staffHeaderRow = new XRTableRow();
            staffHeaderRow.HeightF = 25F;

            var staffIdHeader = new XRTableCell { Text = "Staff ID", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var staffFirstNameHeader = new XRTableCell { Text = "First Name", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var staffLastNameHeader = new XRTableCell { Text = "Last Name", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var roleHeader = new XRTableCell { Text = "Role", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var departmentHeader = new XRTableCell { Text = "Department", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };

            staffHeaderRow.Cells.AddRange(new XRTableCell[] { staffIdHeader, staffFirstNameHeader, staffLastNameHeader, roleHeader, departmentHeader });
            staffHeaderTable.Rows.Add(staffHeaderRow);
            staffHeader.Controls.Add(staffHeaderTable);

            // Staff detail band with data bindings
            var staffDetail = new DetailBand();
            staffDetail.HeightF = 25F;
            staffDetail.Name = "StaffDetail";

            var staffDataTable = new XRTable();
            staffDataTable.LocationF = new PointF(0F, 0F);
            staffDataTable.SizeF = new SizeF(650F, 25F);
            staffDataTable.Name = "StaffDataTable";

            var staffDataRow = new XRTableRow();
            staffDataRow.HeightF = 25F;

            var staffIdCell = new XRTableCell();
            staffIdCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[StaffId]"));

            var staffFirstNameCell = new XRTableCell();
            staffFirstNameCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[FirstName]"));

            var staffLastNameCell = new XRTableCell();
            staffLastNameCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[LastName]"));

            var roleCell = new XRTableCell();
            roleCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[Role]"));

            var departmentCell = new XRTableCell();
            departmentCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[Department]"));

            staffDataRow.Cells.AddRange(new XRTableCell[] { staffIdCell, staffFirstNameCell, staffLastNameCell, roleCell, departmentCell });
            staffDataTable.Rows.Add(staffDataRow);
            staffDetail.Controls.Add(staffDataTable);

            staffDetailReport.Bands.Add(staffHeader);
            staffDetailReport.Bands.Add(staffDetail);
            this.Bands.Add(staffDetailReport);

            // ======================================================================
            // ASSESSMENT SECTION - Using DetailReportBand with its own DataSource
            // ======================================================================
            var assessmentDetailReport = new DetailReportBand();
            assessmentDetailReport.Name = "AssessmentDetailReport";
            assessmentDetailReport.DataSource = assessmentDataSource;
            assessmentDetailReport.DataMember = string.Empty;
            assessmentDetailReport.Level = 1;

            // Assessment section header
            var assessmentHeader = new GroupHeaderBand();
            assessmentHeader.HeightF = 50F;
            assessmentHeader.Name = "AssessmentGroupHeader";
            assessmentHeader.RepeatEveryPage = true;

            var assessmentSectionLabel = new XRLabel();
            assessmentSectionLabel.Text = "ASSESSMENTS";
            assessmentSectionLabel.SizeF = new SizeF(650F, 25F);
            assessmentSectionLabel.LocationF = new PointF(0F, 0F);
            assessmentSectionLabel.Font = new Font("Arial", 14F, FontStyle.Bold);
            assessmentSectionLabel.BackColor = Color.LightCoral;
            assessmentHeader.Controls.Add(assessmentSectionLabel);

            // Assessment table header
            var assessmentHeaderTable = new XRTable();
            assessmentHeaderTable.LocationF = new PointF(0F, 25F);
            assessmentHeaderTable.SizeF = new SizeF(650F, 25F);
            assessmentHeaderTable.Name = "AssessmentHeaderTable";

            var assessmentHeaderRow = new XRTableRow();
            assessmentHeaderRow.HeightF = 25F;

            var assessmentIdHeader = new XRTableCell { Text = "Assessment ID", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var assessmentPupilIdHeader = new XRTableCell { Text = "Pupil ID", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var subjectHeader = new XRTableCell { Text = "Subject", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var scoreHeader = new XRTableCell { Text = "Score", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var dateHeader = new XRTableCell { Text = "Date", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };

            assessmentHeaderRow.Cells.AddRange(new XRTableCell[] { assessmentIdHeader, assessmentPupilIdHeader, subjectHeader, scoreHeader, dateHeader });
            assessmentHeaderTable.Rows.Add(assessmentHeaderRow);
            assessmentHeader.Controls.Add(assessmentHeaderTable);

            // Assessment detail band with data bindings
            var assessmentDetail = new DetailBand();
            assessmentDetail.HeightF = 25F;
            assessmentDetail.Name = "AssessmentDetail";

            var assessmentDataTable = new XRTable();
            assessmentDataTable.LocationF = new PointF(0F, 0F);
            assessmentDataTable.SizeF = new SizeF(650F, 25F);
            assessmentDataTable.Name = "AssessmentDataTable";

            var assessmentDataRow = new XRTableRow();
            assessmentDataRow.HeightF = 25F;

            var assessmentIdCell = new XRTableCell();
            assessmentIdCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[AssessmentId]"));

            var assessmentPupilIdCell = new XRTableCell();
            assessmentPupilIdCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[PupilId]"));

            var subjectCell = new XRTableCell();
            subjectCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[Subject]"));

            var scoreCell = new XRTableCell();
            scoreCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[Score]"));

            var dateCell = new XRTableCell();
            dateCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[Date]"));

            assessmentDataRow.Cells.AddRange(new XRTableCell[] { assessmentIdCell, assessmentPupilIdCell, subjectCell, scoreCell, dateCell });
            assessmentDataTable.Rows.Add(assessmentDataRow);
            assessmentDetail.Controls.Add(assessmentDataTable);

            assessmentDetailReport.Bands.Add(assessmentHeader);
            assessmentDetailReport.Bands.Add(assessmentDetail);
            this.Bands.Add(assessmentDetailReport);
        }
    }
}
