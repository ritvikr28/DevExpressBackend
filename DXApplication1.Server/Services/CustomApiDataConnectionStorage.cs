#nullable enable
using DevExpress.DataAccess.Json;
using DevExpress.DataAccess.Native.Json;
using DevExpress.DataAccess.Web;
using DevExpress.DataAccess.Wizard.Services;
using DXApplication1.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace DXApplication1.Services
{
    public class CustomApiDataConnectionStorage : IDataSourceWizardJsonConnectionStorage
    {
        private static readonly string _connectionsPath = "Data/api-connections.json";
        private readonly string _baseUrl;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomApiDataConnectionStorage(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _baseUrl = (configuration["ReportingBaseUrl"] ?? "https://localhost:44369").TrimEnd('/');
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Extracts the Bearer token from the current HTTP request, if any.
        /// Returns null when called outside of an HTTP request context.
        /// </summary>
        public string? GetBearerToken()
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                return authHeader.Substring("Bearer ".Length).Trim();
            return null;
        }

        private List<JsonDataConnectionDescription> LoadConnections()
        {
            if (!File.Exists(_connectionsPath))
                return new List<JsonDataConnectionDescription>();

            var json = File.ReadAllText(_connectionsPath)
                .Replace("{baseUrl}", _baseUrl);
            return JsonSerializer.Deserialize<List<JsonDataConnectionDescription>>(json)
                ?? new List<JsonDataConnectionDescription>();
        }

        public List<JsonDataConnectionDescription> GetConnections()
        {
            return LoadConnections();
        }

        bool IJsonConnectionStorageService.CanSaveConnection => false;

        bool IJsonConnectionStorageService.ContainsConnection(string connectionName)
        {
            return LoadConnections().Any(x => x.Name == connectionName);
        }

        IEnumerable<JsonDataConnection> IJsonConnectionStorageService.GetConnections()
        {
            var token = GetBearerToken();
            return LoadConnections().Select(x => CreateJsonDataConnection(x, token));
        }

        JsonDataConnection IJsonDataConnectionProviderService.GetJsonDataConnection(string name)
        {
            var connection = LoadConnections().FirstOrDefault(x => x.Name == name);
            if (connection == null)
                throw new KeyNotFoundException();
            return CreateJsonDataConnection(connection, GetBearerToken());
        }

        void IJsonConnectionStorageService.SaveConnection(string connectionName, JsonDataConnection dataConnection, bool saveCredentials)
        {
            throw new System.NotSupportedException();
        }

        /// <summary>
        /// Creates a <see cref="JsonDataConnection"/> from a <see cref="JsonDataConnectionDescription"/>.
        /// When <paramref name="bearerToken"/> is provided and the connection string specifies a URI,
        /// a <see cref="UriJsonSource"/> with an Authorization header is used so that the server-side
        /// DevExpress report engine forwards the caller's token when fetching JSON data.
        /// 
        /// When <paramref name="isSchemaCall"/> is false, the IsSchemaCall query parameter is removed from the URI
        /// to fetch actual data instead of just schema information.
        /// </summary>
        public static JsonDataConnection CreateJsonDataConnection(
            JsonDataConnectionDescription dataConnection, 
            string? bearerToken = null,
            bool isSchemaCall = true)
        {
            if (!string.IsNullOrEmpty(bearerToken))
            {
                var (uri, _) = ParseConnectionString(dataConnection.ConnectionString);
                if (uri is not null)
                {
                    // Modify the URI based on isSchemaCall flag
                    var modifiedUri = ModifyUriForSchemaCall(uri, isSchemaCall);
                    
                    var uriSource = new UriJsonSource(modifiedUri);
                    uriSource.HeaderParameters.Add(new HeaderParameter("Authorization", $"Bearer {bearerToken}"));
                    return new JsonDataConnection(uriSource)
                    {
                        StoreConnectionNameOnly = true,
                        Name = dataConnection.Name
                    };
                }
            }

            // Fall back to plain connection string when no token is available or the
            // connection string is not a URI-based one.
            // For non-token connections, also modify the connection string if needed
            var modifiedConnectionString = ModifyConnectionStringForSchemaCall(dataConnection.ConnectionString, isSchemaCall);
            return new JsonDataConnection(modifiedConnectionString)
            {
                StoreConnectionNameOnly = true,
                Name = dataConnection.Name
            };
        }

        /// <summary>
        /// Modifies the URI to add or remove the IsSchemaCall query parameter.
        /// </summary>
        private static Uri ModifyUriForSchemaCall(Uri uri, bool isSchemaCall)
        {
            var uriBuilder = new UriBuilder(uri);
            var queryParams = ParseQueryString(uriBuilder.Query);

            if (isSchemaCall)
            {
                // Ensure IsSchemaCall=true is in the query
                queryParams["IsSchemaCall"] = "true";
            }
            else
            {
                // Remove IsSchemaCall parameter to fetch actual data
                queryParams.Remove("IsSchemaCall");
            }

            uriBuilder.Query = BuildQueryString(queryParams);
            return uriBuilder.Uri;
        }

        /// <summary>
        /// Parses a query string into a dictionary of key-value pairs.
        /// </summary>
        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            if (string.IsNullOrEmpty(query))
                return result;

            // Remove leading '?' if present
            var queryString = query.TrimStart('?');
            
            foreach (var pair in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = pair.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = Uri.UnescapeDataString(pair.Substring(0, eqIndex));
                    var value = eqIndex < pair.Length - 1 
                        ? Uri.UnescapeDataString(pair.Substring(eqIndex + 1)) 
                        : string.Empty;
                    result[key] = value;
                }
                else if (pair.Length > 0)
                {
                    result[Uri.UnescapeDataString(pair)] = string.Empty;
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a query string from a dictionary of key-value pairs.
        /// </summary>
        private static string BuildQueryString(Dictionary<string, string> queryParams)
        {
            if (queryParams.Count == 0)
                return string.Empty;

            var pairs = queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
            return string.Join("&", pairs);
        }

        /// <summary>
        /// Modifies the connection string to add or remove the IsSchemaCall query parameter.
        /// Note: Empty RootElement= is preserved as it's a DevExpress convention for JSON array roots.
        /// </summary>
        private static string ModifyConnectionStringForSchemaCall(string connectionString, bool isSchemaCall)
        {
            var (uri, rootElement) = ParseConnectionString(connectionString);
            if (uri == null)
                return connectionString;

            var modifiedUri = ModifyUriForSchemaCall(uri, isSchemaCall);
            
            // Reconstruct the connection string.
            // Note: RootElement= (empty) is intentionally preserved as it's a DevExpress convention
            // indicating the JSON root is an array. Omitting it could cause parsing issues.
            var result = $"Uri={modifiedUri}";
            if (!string.IsNullOrEmpty(rootElement))
                result += $";RootElement={rootElement}";
            else
                result += ";RootElement=";
            
            return result;
        }

        /// <summary>
        /// Kept for backwards compatibility with any external callers.
        /// </summary>
        public static JsonDataConnection CreateJsonDataConnectionFromString(JsonDataConnectionDescription dataConnection)
            => CreateJsonDataConnection(dataConnection, bearerToken: null);

        /// <summary>
        /// Parses a DevExpress JSON connection string of the form
        /// <c>Uri=https://...;RootElement=items</c> and returns the absolute URI if found.
        /// The RootElement value is also returned but is typically empty in this codebase.
        /// </summary>
        private static (Uri? uri, string? rootElement) ParseConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return (null, null);

            string? uriString = null;
            string? rootElement = null;

            foreach (var segment in connectionString.Split(';'))
            {
                var eq = segment.IndexOf('=');
                if (eq < 0) continue;
                var key = segment.Substring(0, eq).Trim();
                var value = segment.Substring(eq + 1).Trim();

                if (key.Equals("Uri", StringComparison.OrdinalIgnoreCase))
                    uriString = value;
                else if (key.Equals("RootElement", StringComparison.OrdinalIgnoreCase))
                    rootElement = value;
            }

            if (string.IsNullOrEmpty(uriString))
                return (null, rootElement);

            return Uri.TryCreate(uriString, UriKind.Absolute, out var uri)
                ? (uri, rootElement)
                : (null, rootElement);
        }
    }
}
