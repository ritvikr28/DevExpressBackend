using DevExpress.DataAccess.Json;
using DevExpress.XtraReports.Parameters;
using DevExpress.XtraReports.UI;
using System.Drawing;

namespace DXApplication1.PredefinedReports
{
    /// <summary>
    /// A pupil report that displays one pupil per page using GroupHeaderBand with PageBreak.
    /// 
    /// This report demonstrates the "Page Break per Pupil" pattern:
    /// - Groups data by identity.learnerExternalId (pupil ID)
    /// - Sets PageBreak = AfterBand on the GroupHeaderBand
    /// - Each pupil's data appears on a separate page within one document
    /// 
    /// Data Structure Expected:
    /// The report expects a JSON array of learners with nested objects:
    /// - identity: learnerExternalId, preferredName, preferredForename, preferredSurname, etc.
    /// - personal: dateOfBirth, gender, personalPronoun, etc.
    /// - enrollment: admissionNumber, admissionDate, onRollState, etc.
    /// - attendance: percentagePresent, totalSessions, etc.
    /// - assessment: overallGrade, predictedGrade, etc.
    /// </summary>
    public partial class PupilReport : XtraReport
    {
        public PupilReport()
        {
            InitializeComponent();
            SetupDataSourceAndBands();
        }

        /// <summary>
        /// Sets up the JsonDataSource and report bands with grouping by learnerExternalId
        /// and page breaks after each group (pupil).
        /// </summary>
        private void SetupDataSourceAndBands()
        {
            // ======================================================================
            // JSON DATA SOURCE - Uses Learners connection for design-time schema
            // At runtime, CustomReportStorageWebExtension injects live data
            // ======================================================================
            var jsonDataSource = new JsonDataSource
            {
                Name = "LearnersDataSource"
            };
            
            // Use in-memory sample data for schema discovery in designer
            // This will be replaced with live data at runtime by InjectLiveData
            const string sampleJson = """
                [
                  {
                    "identity": {
                      "learnerExternalId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                      "preferredName": "Emma Thompson",
                      "preferredForename": "Emma",
                      "preferredSurname": "Thompson",
                      "legalName": "Emma Thompson",
                      "legalForename": "Emma",
                      "legalSurname": "Thompson"
                    },
                    "personal": {
                      "dateOfBirth": "2010-03-15",
                      "gender": "Female",
                      "personalPronoun": "She/Her",
                      "englishAdditionalLanguage": false,
                      "imagePath": ""
                    },
                    "enrollment": {
                      "admissionNumber": "A001234",
                      "admissionDate": "2018-09-04",
                      "leavingDate": null,
                      "onRollState": "OnRoll",
                      "enrollmentStatus": "Active"
                    }
                  }
                ]
                """;

            jsonDataSource.JsonSource = new CustomJsonSource(sampleJson);

            this.ComponentStorage.Add(jsonDataSource);
            this.DataSource = jsonDataSource;
            this.DataMember = string.Empty;

            // ======================================================================
            // REPORT PARAMETERS
            // ======================================================================
            var userIdParam = new Parameter
            {
                Name = "userId",
                Description = "Pupil ID(s) - comma-separated for multiple (e.g. id1,id2,id3)",
                Type = typeof(string),
                Visible = true
            };
            this.Parameters.Add(userIdParam);

            // ======================================================================
            // MARGINS
            // ======================================================================
            var topMargin = new TopMarginBand();
            topMargin.HeightF = 50F;
            topMargin.Name = "TopMargin";
            this.Bands.Add(topMargin);

            var bottomMargin = new BottomMarginBand();
            bottomMargin.HeightF = 50F;
            bottomMargin.Name = "BottomMargin";
            this.Bands.Add(bottomMargin);

            // ======================================================================
            // REPORT HEADER - Shows once at the beginning of the report
            // ======================================================================
            var reportHeader = new ReportHeaderBand();
            reportHeader.HeightF = 60F;
            reportHeader.Name = "ReportHeader";

            var titleLabel = new XRLabel();
            titleLabel.Text = "PUPIL REPORT";
            titleLabel.SizeF = new SizeF(650F, 35F);
            titleLabel.LocationF = new PointF(0F, 5F);
            titleLabel.Font = new Font("Arial", 20F, FontStyle.Bold);
            titleLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            reportHeader.Controls.Add(titleLabel);

            var subtitleLabel = new XRLabel();
            subtitleLabel.Text = "Individual Pupil Details (One Pupil Per Page)";
            subtitleLabel.SizeF = new SizeF(650F, 20F);
            subtitleLabel.LocationF = new PointF(0F, 40F);
            subtitleLabel.Font = new Font("Arial", 10F, FontStyle.Italic);
            subtitleLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            subtitleLabel.ForeColor = Color.Gray;
            reportHeader.Controls.Add(subtitleLabel);

            this.Bands.Add(reportHeader);

            // ======================================================================
            // GROUP HEADER BAND - Grouped by learnerExternalId with PAGE BREAK
            // This creates a new page for each pupil
            // ======================================================================
            var groupHeader = new GroupHeaderBand();
            groupHeader.HeightF = 180F;
            groupHeader.Name = "PupilGroupHeader";
            
            // KEY SETTING: Group by learnerExternalId
            groupHeader.GroupFields.Add(new GroupField("identity.learnerExternalId"));
            
            // KEY SETTING: Page break AFTER each group (except last)
            // This ensures each pupil starts on a new page
            groupHeader.PageBreak = PageBreak.BeforeBandExceptFirstEntry;
            
            // Pupil Name Header (Large)
            var pupilNameLabel = new XRLabel();
            pupilNameLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[identity.preferredName]"));
            pupilNameLabel.SizeF = new SizeF(500F, 40F);
            pupilNameLabel.LocationF = new PointF(0F, 5F);
            pupilNameLabel.Font = new Font("Arial", 18F, FontStyle.Bold);
            pupilNameLabel.ForeColor = Color.DarkBlue;
            groupHeader.Controls.Add(pupilNameLabel);

            // Pupil ID Label
            var pupilIdLabel = new XRLabel();
            pupilIdLabel.Text = "Pupil ID:";
            pupilIdLabel.SizeF = new SizeF(80F, 20F);
            pupilIdLabel.LocationF = new PointF(0F, 50F);
            pupilIdLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            groupHeader.Controls.Add(pupilIdLabel);

            var pupilIdValue = new XRLabel();
            pupilIdValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[identity.learnerExternalId]"));
            pupilIdValue.SizeF = new SizeF(400F, 20F);
            pupilIdValue.LocationF = new PointF(80F, 50F);
            pupilIdValue.Font = new Font("Arial", 9F);
            groupHeader.Controls.Add(pupilIdValue);

            // ======================================================================
            // IDENTITY SECTION
            // ======================================================================
            var identitySectionLabel = new XRLabel();
            identitySectionLabel.Text = "IDENTITY INFORMATION";
            identitySectionLabel.SizeF = new SizeF(650F, 25F);
            identitySectionLabel.LocationF = new PointF(0F, 80F);
            identitySectionLabel.Font = new Font("Arial", 11F, FontStyle.Bold);
            identitySectionLabel.BackColor = Color.LightSteelBlue;
            identitySectionLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            identitySectionLabel.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 0, 0, 0);
            groupHeader.Controls.Add(identitySectionLabel);

            // Legal Name
            var legalNameLabel = new XRLabel();
            legalNameLabel.Text = "Legal Name:";
            legalNameLabel.SizeF = new SizeF(120F, 20F);
            legalNameLabel.LocationF = new PointF(0F, 110F);
            legalNameLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            groupHeader.Controls.Add(legalNameLabel);

            var legalNameValue = new XRLabel();
            legalNameValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[identity.legalName]"));
            legalNameValue.SizeF = new SizeF(200F, 20F);
            legalNameValue.LocationF = new PointF(120F, 110F);
            legalNameValue.Font = new Font("Arial", 9F);
            groupHeader.Controls.Add(legalNameValue);

            // Preferred Name
            var preferredNameLabel = new XRLabel();
            preferredNameLabel.Text = "Preferred Name:";
            preferredNameLabel.SizeF = new SizeF(120F, 20F);
            preferredNameLabel.LocationF = new PointF(330F, 110F);
            preferredNameLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            groupHeader.Controls.Add(preferredNameLabel);

            var preferredNameValue = new XRLabel();
            preferredNameValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[identity.preferredName]"));
            preferredNameValue.SizeF = new SizeF(200F, 20F);
            preferredNameValue.LocationF = new PointF(450F, 110F);
            preferredNameValue.Font = new Font("Arial", 9F);
            groupHeader.Controls.Add(preferredNameValue);

            // ======================================================================
            // PERSONAL SECTION
            // ======================================================================
            var personalSectionLabel = new XRLabel();
            personalSectionLabel.Text = "PERSONAL INFORMATION";
            personalSectionLabel.SizeF = new SizeF(650F, 25F);
            personalSectionLabel.LocationF = new PointF(0F, 140F);
            personalSectionLabel.Font = new Font("Arial", 11F, FontStyle.Bold);
            personalSectionLabel.BackColor = Color.LightGreen;
            personalSectionLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            personalSectionLabel.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 0, 0, 0);
            groupHeader.Controls.Add(personalSectionLabel);

            this.Bands.Add(groupHeader);

            // ======================================================================
            // DETAIL BAND - Personal/Enrollment details per pupil
            // ======================================================================
            var detailBand = new DetailBand();
            detailBand.HeightF = 100F;
            detailBand.Name = "Detail";

            // Date of Birth
            var dobLabel = new XRLabel();
            dobLabel.Text = "Date of Birth:";
            dobLabel.SizeF = new SizeF(120F, 20F);
            dobLabel.LocationF = new PointF(0F, 5F);
            dobLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            detailBand.Controls.Add(dobLabel);

            var dobValue = new XRLabel();
            dobValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[personal.dateOfBirth]"));
            dobValue.SizeF = new SizeF(150F, 20F);
            dobValue.LocationF = new PointF(120F, 5F);
            dobValue.Font = new Font("Arial", 9F);
            detailBand.Controls.Add(dobValue);

            // Gender
            var genderLabel = new XRLabel();
            genderLabel.Text = "Gender:";
            genderLabel.SizeF = new SizeF(120F, 20F);
            genderLabel.LocationF = new PointF(330F, 5F);
            genderLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            detailBand.Controls.Add(genderLabel);

            var genderValue = new XRLabel();
            genderValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[personal.gender]"));
            genderValue.SizeF = new SizeF(150F, 20F);
            genderValue.LocationF = new PointF(450F, 5F);
            genderValue.Font = new Font("Arial", 9F);
            detailBand.Controls.Add(genderValue);

            // Pronoun
            var pronounLabel = new XRLabel();
            pronounLabel.Text = "Pronoun:";
            pronounLabel.SizeF = new SizeF(120F, 20F);
            pronounLabel.LocationF = new PointF(0F, 30F);
            pronounLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            detailBand.Controls.Add(pronounLabel);

            var pronounValue = new XRLabel();
            pronounValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[personal.personalPronoun]"));
            pronounValue.SizeF = new SizeF(150F, 20F);
            pronounValue.LocationF = new PointF(120F, 30F);
            pronounValue.Font = new Font("Arial", 9F);
            detailBand.Controls.Add(pronounValue);

            // ======================================================================
            // ENROLLMENT SECTION (in Detail Band)
            // ======================================================================
            var enrollmentSectionLabel = new XRLabel();
            enrollmentSectionLabel.Text = "ENROLLMENT INFORMATION";
            enrollmentSectionLabel.SizeF = new SizeF(650F, 25F);
            enrollmentSectionLabel.LocationF = new PointF(0F, 55F);
            enrollmentSectionLabel.Font = new Font("Arial", 11F, FontStyle.Bold);
            enrollmentSectionLabel.BackColor = Color.LightCoral;
            enrollmentSectionLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            enrollmentSectionLabel.Padding = new DevExpress.XtraPrinting.PaddingInfo(5, 0, 0, 0);
            detailBand.Controls.Add(enrollmentSectionLabel);

            // Admission Number
            var admissionLabel = new XRLabel();
            admissionLabel.Text = "Admission No:";
            admissionLabel.SizeF = new SizeF(120F, 20F);
            admissionLabel.LocationF = new PointF(0F, 80F);
            admissionLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            detailBand.Controls.Add(admissionLabel);

            var admissionValue = new XRLabel();
            admissionValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[enrollment.admissionNumber]"));
            admissionValue.SizeF = new SizeF(150F, 20F);
            admissionValue.LocationF = new PointF(120F, 80F);
            admissionValue.Font = new Font("Arial", 9F);
            detailBand.Controls.Add(admissionValue);

            // Status
            var statusLabel = new XRLabel();
            statusLabel.Text = "Status:";
            statusLabel.SizeF = new SizeF(120F, 20F);
            statusLabel.LocationF = new PointF(330F, 80F);
            statusLabel.Font = new Font("Arial", 9F, FontStyle.Bold);
            detailBand.Controls.Add(statusLabel);

            var statusValue = new XRLabel();
            statusValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[enrollment.enrollmentStatus]"));
            statusValue.SizeF = new SizeF(150F, 20F);
            statusValue.LocationF = new PointF(450F, 80F);
            statusValue.Font = new Font("Arial", 9F);
            detailBand.Controls.Add(statusValue);

            this.Bands.Add(detailBand);

            // ======================================================================
            // GROUP FOOTER - Optional summary per pupil
            // ======================================================================
            var groupFooter = new GroupFooterBand();
            groupFooter.HeightF = 30F;
            groupFooter.Name = "PupilGroupFooter";

            var footerLine = new XRLine();
            footerLine.SizeF = new SizeF(650F, 2F);
            footerLine.LocationF = new PointF(0F, 10F);
            footerLine.ForeColor = Color.Gray;
            groupFooter.Controls.Add(footerLine);

            this.Bands.Add(groupFooter);

            // ======================================================================
            // PAGE FOOTER - Page number on every page
            // ======================================================================
            var pageFooter = new PageFooterBand();
            pageFooter.HeightF = 40F;
            pageFooter.Name = "PageFooter";

            var pageInfo = new XRPageInfo();
            pageInfo.Format = "Page {0} of {1}";
            pageInfo.SizeF = new SizeF(200F, 20F);
            pageInfo.LocationF = new PointF(225F, 10F);
            pageInfo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            pageInfo.Font = new Font("Arial", 9F);
            pageFooter.Controls.Add(pageInfo);

            // Generated timestamp
            var timestampInfo = new XRPageInfo();
            timestampInfo.PageInfo = DevExpress.XtraPrinting.PageInfo.DateTime;
            timestampInfo.Format = "Generated: {0:dd/MM/yyyy HH:mm}";
            timestampInfo.SizeF = new SizeF(200F, 20F);
            timestampInfo.LocationF = new PointF(450F, 10F);
            timestampInfo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            timestampInfo.Font = new Font("Arial", 8F);
            timestampInfo.ForeColor = Color.Gray;
            pageFooter.Controls.Add(timestampInfo);

            this.Bands.Add(pageFooter);

            // ======================================================================
            // REPORT PROPERTIES
            // ======================================================================
            this.PageWidth = 850;
            this.PageHeight = 1100;
            this.Margins = new System.Drawing.Printing.Margins(50, 50, 50, 50);
        }
    }
}
