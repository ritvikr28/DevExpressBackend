using DevExpress.AspNetCore.Reporting.QueryBuilder;
using DevExpress.AspNetCore.Reporting.QueryBuilder.Native.Services;
using DevExpress.AspNetCore.Reporting.ReportDesigner;
using DevExpress.AspNetCore.Reporting.ReportDesigner.Native.Services;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer.Native.Services;
using DevExpress.DataAccess.Json;
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
        public IActionResult GetDesignerModel(
            [FromForm] string reportUrl,
            [FromServices] IReportDesignerModelBuilder designerModelBuilder,
            [FromForm] ReportDesignerSettingsBase designerModelSettings)
        {
            // Sample learners structured as domain-grouped nested objects.
            // Each nested object becomes a collapsible folder in the DevExpress field list.
            // Add new domain sections here (e.g. attendance, assessment) to expose
            // their fields in the designer before the real API integration is done.
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
                    },
                    "attendance": {
                      "percentagePresent": 96.5,
                      "totalSessions": 200,
                      "authorisedAbsences": 4,
                      "unauthorisedAbsences": 4
                    },
                    "assessment": {
                      "overallGrade": "B",
                      "predictedGrade": "A",
                      "lastAssessmentDate": "2024-01-15"
                    }
                  },
                  {
                    "identity": {
                      "learnerExternalId": "9b2e1c44-8a3d-4f7e-bb21-1234abcd5678",
                      "preferredName": "Oliver Patel",
                      "preferredForename": "Oliver",
                      "preferredSurname": "Patel",
                      "legalName": "Oliver Patel",
                      "legalForename": "Oliver",
                      "legalSurname": "Patel"
                    },
                    "personal": {
                      "dateOfBirth": "2011-07-22",
                      "gender": "Male",
                      "personalPronoun": "He/Him",
                      "englishAdditionalLanguage": true,
                      "imagePath": ""
                    },
                    "enrollment": {
                      "admissionNumber": "A001235",
                      "admissionDate": "2019-09-03",
                      "leavingDate": null,
                      "onRollState": "OnRoll",
                      "enrollmentStatus": "Active"
                    },
                    "attendance": {
                      "percentagePresent": 88.0,
                      "totalSessions": 200,
                      "authorisedAbsences": 16,
                      "unauthorisedAbsences": 8
                    },
                    "assessment": {
                      "overallGrade": "C",
                      "predictedGrade": "B",
                      "lastAssessmentDate": "2024-01-15"
                    }
                  },
                  {
                    "identity": {
                      "learnerExternalId": "c7d3e821-4b5a-49f2-a1c6-8765fedc3210",
                      "preferredName": "Sophia Okafor",
                      "preferredForename": "Sophia",
                      "preferredSurname": "Okafor",
                      "legalName": "Sophia Okafor",
                      "legalForename": "Sophia",
                      "legalSurname": "Okafor"
                    },
                    "personal": {
                      "dateOfBirth": "2009-11-08",
                      "gender": "Female",
                      "personalPronoun": "She/Her",
                      "englishAdditionalLanguage": false,
                      "imagePath": ""
                    },
                    "enrollment": {
                      "admissionNumber": "A001236",
                      "admissionDate": "2017-09-05",
                      "leavingDate": null,
                      "onRollState": "OnRoll",
                      "enrollmentStatus": "Active"
                    },
                    "attendance": {
                      "percentagePresent": 99.0,
                      "totalSessions": 200,
                      "authorisedAbsences": 2,
                      "unauthorisedAbsences": 0
                    },
                    "assessment": {
                      "overallGrade": "A",
                      "predictedGrade": "A*",
                      "lastAssessmentDate": "2024-01-15"
                    }
                  }
                ]
                """;

            var ds = new JsonDataSource { JsonSource = new CustomJsonSource(sampleJson) };
            ds.Fill();

            var designerModel = designerModelBuilder.Report(reportUrl)
                .DataSources(dataSources => dataSources.Add("Learners", ds))
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
