using DevExpress.XtraReports.UI;
using System.ComponentModel;
using System.Drawing;

namespace DXApplication1.PredefinedReports
{
    partial class MultiSourceReport
    {
        private IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Designer generated code

        private void InitializeComponent()
        {
            this.components = new Container();
            
            // TopMargin
            this.TopMargin = new TopMarginBand();
            this.TopMargin.HeightF = 50F;
            this.TopMargin.Name = "TopMargin";

            // BottomMargin
            this.BottomMargin = new BottomMarginBand();
            this.BottomMargin.HeightF = 50F;
            this.BottomMargin.Name = "BottomMargin";

            // Report Header
            this.ReportHeader = new ReportHeaderBand();
            this.ReportHeader.HeightF = 60F;
            this.ReportHeader.Name = "ReportHeader";
            
            var titleLabel = new XRLabel();
            titleLabel.Text = "Multi-Source Report";
            titleLabel.SizeF = new SizeF(650F, 30F);
            titleLabel.LocationF = new PointF(0F, 10F);
            titleLabel.Font = new Font("Arial", 18F, FontStyle.Bold);
            this.ReportHeader.Controls.Add(titleLabel);

            var infoLabel = new XRLabel();
            infoLabel.Text = "This report demonstrates data from multiple JSON sources (Pupil, Staff, Assessment)";
            infoLabel.SizeF = new SizeF(650F, 20F);
            infoLabel.LocationF = new PointF(0F, 40F);
            infoLabel.Font = new Font("Arial", 10F, FontStyle.Italic);
            this.ReportHeader.Controls.Add(infoLabel);

            // ======================================================================
            // PUPIL SECTION - Bound to the primary DataSource (PupilDataSource)
            // ======================================================================
            
            // Pupil Group Header Band
            this.PupilGroupHeader = new GroupHeaderBand();
            this.PupilGroupHeader.HeightF = 50F;
            this.PupilGroupHeader.Name = "PupilGroupHeader";
            this.PupilGroupHeader.RepeatEveryPage = true;
            
            var pupilSectionLabel = new XRLabel();
            pupilSectionLabel.Text = "PUPILS";
            pupilSectionLabel.SizeF = new SizeF(650F, 25F);
            pupilSectionLabel.LocationF = new PointF(0F, 0F);
            pupilSectionLabel.Font = new Font("Arial", 14F, FontStyle.Bold);
            pupilSectionLabel.BackColor = Color.LightBlue;
            this.PupilGroupHeader.Controls.Add(pupilSectionLabel);
            
            // Pupil table header row
            var pupilHeaderTable = new XRTable();
            pupilHeaderTable.LocationF = new PointF(0F, 25F);
            pupilHeaderTable.SizeF = new SizeF(650F, 25F);
            pupilHeaderTable.Name = "PupilHeaderTable";
            
            var pupilHeaderRow = new XRTableRow();
            pupilHeaderRow.HeightF = 25F;
            
            var pupilIdHeader = new XRTableCell { Text = "Pupil ID", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var firstNameHeader = new XRTableCell { Text = "First Name", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var lastNameHeader = new XRTableCell { Text = "Last Name", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var dobHeader = new XRTableCell { Text = "Date of Birth", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            var classHeader = new XRTableCell { Text = "Class", Font = new Font("Arial", 10F, FontStyle.Bold), BackColor = Color.LightGray };
            
            pupilHeaderRow.Cells.AddRange(new XRTableCell[] { pupilIdHeader, firstNameHeader, lastNameHeader, dobHeader, classHeader });
            pupilHeaderTable.Rows.Add(pupilHeaderRow);
            this.PupilGroupHeader.Controls.Add(pupilHeaderTable);

            // Detail band - Pupil data row (iterates over primary DataSource)
            this.Detail = new DetailBand();
            this.Detail.HeightF = 25F;
            this.Detail.Name = "Detail";
            
            var pupilDataTable = new XRTable();
            pupilDataTable.LocationF = new PointF(0F, 0F);
            pupilDataTable.SizeF = new SizeF(650F, 25F);
            pupilDataTable.Name = "PupilDataTable";
            
            var pupilDataRow = new XRTableRow();
            pupilDataRow.HeightF = 25F;
            
            var pupilIdCell = new XRTableCell();
            pupilIdCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[PupilId]"));
            
            var firstNameCell = new XRTableCell();
            firstNameCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[FirstName]"));
            
            var lastNameCell = new XRTableCell();
            lastNameCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[LastName]"));
            
            var dobCell = new XRTableCell();
            dobCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[DateOfBirth]"));
            
            var classCell = new XRTableCell();
            classCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[Class]"));
            
            pupilDataRow.Cells.AddRange(new XRTableCell[] { pupilIdCell, firstNameCell, lastNameCell, dobCell, classCell });
            pupilDataTable.Rows.Add(pupilDataRow);
            this.Detail.Controls.Add(pupilDataTable);

            // Page Footer with page info
            this.PageFooter = new PageFooterBand();
            this.PageFooter.HeightF = 50F;
            this.PageFooter.Name = "PageFooter";

            var pageInfo = new XRPageInfo();
            pageInfo.Format = "Page {0} of {1}";
            pageInfo.LocationF = new PointF(0F, 10F);
            pageInfo.SizeF = new SizeF(300F, 20F);
            this.PageFooter.Controls.Add(pageInfo);

            // Configure report bands
            this.Bands.AddRange(new Band[] {
                this.TopMargin,
                this.ReportHeader,
                this.PupilGroupHeader,
                this.Detail,
                this.PageFooter,
                this.BottomMargin
            });

            this.PageWidth = 850;
            this.PageHeight = 1100;
            this.Version = "24.1";
        }

        #endregion

        private TopMarginBand TopMargin;
        private BottomMarginBand BottomMargin;
        private ReportHeaderBand ReportHeader;
        private GroupHeaderBand PupilGroupHeader;
        private DetailBand Detail;
        private PageFooterBand PageFooter;
    }
}
