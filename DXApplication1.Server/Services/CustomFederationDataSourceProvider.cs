#nullable enable
using DevExpress.DataAccess.Json;
using DevExpress.DataAccess.Web;
using DXApplication1.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXApplication1.Services
{
    /// <summary>
    /// NOTE: DevExpress Data Federation works automatically with JSON data sources.
    /// When you create a FederationDataSource in a report and reference JsonDataSource objects
    /// with ConnectionName properties, DevExpress resolves those connections through:
    /// - CustomApiDataConnectionStorage (for Report Designer)
    /// - CustomJsonDataConnectionProviderFactory (for Report Viewer)
    /// 
    /// No additional factory registration is needed for Data Federation to work.
    /// The FederationDataSource queries the underlying JsonDataSource objects which
    /// then call the API endpoints defined in api-connections.json.
    /// 
    /// This file is kept for documentation purposes but the classes below are not used
    /// because DevExpress doesn't expose IFederationDataSourceProviderFactory interface.
    /// </summary>
    public static class FederationDataSourceDocumentation
    {
        // Data Federation works as follows:
        // 1. Create JsonDataSource objects with ConnectionName matching api-connections.json entries
        // 2. Add those JsonDataSource objects to the report's ComponentStorage
        // 3. Create a FederationDataSource that references those JsonDataSource objects via Source/SourceNode
        // 4. Define queries using SelectNode and JoinElement
        // 5. DevExpress automatically resolves the JsonDataSource connections at runtime
    }
}
