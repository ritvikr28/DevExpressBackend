using DevExpress.AspNetCore.Reporting.QueryBuilder;
using DevExpress.AspNetCore.Reporting.QueryBuilder.Native.Services;
using DevExpress.AspNetCore.Reporting.ReportDesigner;
using DevExpress.AspNetCore.Reporting.ReportDesigner.Native.Services;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer.Native.Services;
using DevExpress.DataAccess.Sql;
using DevExpress.XtraReports.Web.ClientControls;
using DevExpress.XtraReports.Web.ReportDesigner;
using DevExpress.XtraReports.Web.ReportDesigner.Services;
using ESS.Platform.Authorization.Attributes;
using ESS.Platform.Authorization.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace DXApplication1.Controllers
{
    [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
    public class CustomWebDocumentViewerController : WebDocumentViewerController
    {
        public CustomWebDocumentViewerController(IWebDocumentViewerMvcControllerService controllerService) : base(controllerService)
        {
        }
    }

    [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
    public class CustomReportDesignerController : ReportDesignerController
    {
        public CustomReportDesignerController(IReportDesignerMvcControllerService controllerService) : base(controllerService)
        {
        }

        [HttpPost("[action]")]
        public IActionResult GetDesignerModel([FromForm] string reportUrl, [FromServices] IReportDesignerModelBuilder designerModelBuilder, [FromForm] ReportDesignerSettingsBase designerModelSettings)
        {
            var designerModel = designerModelBuilder.Report(reportUrl)
                .BuildModel();

            designerModel.Assign(designerModelSettings);

            var clientSideModelSettings = new ClientSideModelSettings
            {
                IncludeLocalization = false,
                IncludeCldrData = false,
                IncludeCldrSupplemental = false
            };
            return DesignerModel(designerModel, clientSideModelSettings);
        }
    }

    [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
    public class CustomQueryBuilderController : QueryBuilderController
    {
        public CustomQueryBuilderController(IQueryBuilderMvcControllerService controllerService) : base(controllerService)
        {
        }
    }
}
