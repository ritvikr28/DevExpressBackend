using DevExpress.XtraReports.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DXApplication1.PredefinedReports
{
    public static class ReportsFactory
    {
        public static Dictionary<string, Func<XtraReport>> Reports = new Dictionary<string, Func<XtraReport>>()
        {
            ["TestReport"] = () => new TestReport(),
            ["MultiSourceReport"] = () => new MultiSourceReport(),
            ["FederatedReport"] = () => new FederatedReport(),
            ["PupilReport"] = () => new PupilReport()
        };
    }
}