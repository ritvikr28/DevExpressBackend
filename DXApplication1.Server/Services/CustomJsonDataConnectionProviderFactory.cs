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
    public class CustomJsonDataConnectionProviderFactory : IJsonDataConnectionProviderFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomJsonDataConnectionProviderFactory(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public IJsonDataConnectionProviderService Create()
        {
            var storage = new CustomApiDataConnectionStorage(_configuration, _httpContextAccessor);
            var token = storage.GetBearerToken();
            // Pass isSchemaCall: false so the viewer fetches actual data, not just schema
            return new WebDocumentViewerJsonDataConnectionProvider(storage.GetConnections(), token, isSchemaCall: false);
        }
    }

    public class WebDocumentViewerJsonDataConnectionProvider : IJsonDataConnectionProviderService
    {
        readonly IEnumerable<JsonDataConnectionDescription> jsonDataConnections;
        readonly string? bearerToken;
        readonly bool isSchemaCall;

        public WebDocumentViewerJsonDataConnectionProvider(
            IEnumerable<JsonDataConnectionDescription> jsonDataConnections, 
            string? bearerToken = null,
            bool isSchemaCall = true)
        {
            this.jsonDataConnections = jsonDataConnections;
            this.bearerToken = bearerToken;
            this.isSchemaCall = isSchemaCall;
        }

        public JsonDataConnection GetJsonDataConnection(string name)
        {
            var connection = jsonDataConnections.FirstOrDefault(x => x.Name == name);
            if (connection == null)
                throw new InvalidOperationException($"Connection '{name}' not found.");
            return CustomApiDataConnectionStorage.CreateJsonDataConnection(connection, bearerToken, isSchemaCall);
        }
    }
}
