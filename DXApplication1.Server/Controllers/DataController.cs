#nullable enable
using DXApplication1.Server.Models;
using ESS.Platform.Authorization.Attributes;
using ESS.Platform.Authorization.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DXApplication1.Server.Controllers
{
    [Route("api/v1")]
    [ApiController]
    public class DataController : ControllerBase
    {
        // TODO: Consider moving to configuration (e.g., appsettings.json) for environment flexibility
        private static readonly string ColumnsMetadataPath = "Data/columns-metadata.json";

        /// <summary>
        /// Maps data source name to the corresponding JSON data file path (case-insensitive).
        /// </summary>
        /// <remarks>
        /// TODO: Consider moving data source mappings to configuration to allow adding new sources without code changes.
        /// </remarks>
        private static string? GetDataFilePath(string dataSourceName)
        {
            return dataSourceName.ToLowerInvariant() switch
            {
                "pupil" => "Data/pupil-data.json",
                "staff" => "Data/staff-data.json",
                "assessment" => "Data/assessment-data.json",
                _ => null
            };
        }

        /// <summary>
        /// Parses a JSON element value to a CLR type.
        /// </summary>
        private static object? ParseJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Parses column metadata from a JSON array element.
        /// </summary>
        private static List<ColumnMetadata> ParseColumnMetadata(JsonElement columnsArray)
        {
            var columns = new List<ColumnMetadata>();
            foreach (var col in columnsArray.EnumerateArray())
            {
                columns.Add(new ColumnMetadata
                {
                    Name = col.GetProperty("name").GetString() ?? string.Empty,
                    Type = col.GetProperty("type").GetString() ?? string.Empty
                });
            }
            return columns;
        }

        /// <summary>
        /// Checks if the columns metadata file exists and returns an appropriate error result if not.
        /// </summary>
        private IActionResult? ValidateMetadataFileExists()
        {
            if (!System.IO.File.Exists(ColumnsMetadataPath))
                return NotFound($"Columns metadata file not found: {ColumnsMetadataPath}");
            return null;
        }

        /// <summary>
        /// Retrieves data from a single source with optional column filtering.
        /// </summary>
        private async Task<(List<Dictionary<string, object?>>? data, string? error)> GetDataFromSourceAsync(
            string dataSourceName,
            IEnumerable<string>? columns)
        {
            var fileName = GetDataFilePath(dataSourceName);
            if (fileName == null)
                return (null, $"Data source '{dataSourceName}' is not recognized.");
            
            if (!System.IO.File.Exists(fileName))
                return (null, $"Data file not found for data source '{dataSourceName}': {fileName}");

            using var dataStream = System.IO.File.OpenRead(fileName);
            var dataDoc = await JsonDocument.ParseAsync(dataStream);

            if (dataDoc.RootElement.ValueKind != JsonValueKind.Array)
                return (null, $"Data source '{dataSourceName}' does not contain array data.");

            var columnSet = columns != null && columns.Any()
                ? new HashSet<string>(columns, StringComparer.Ordinal)
                : null;

            var result = new List<Dictionary<string, object?>>();
            foreach (var element in dataDoc.RootElement.EnumerateArray())
            {
                var dict = new Dictionary<string, object?>();
                var props = columnSet != null
                    ? element.EnumerateObject().Where(p => columnSet.Contains(p.Name))
                    : element.EnumerateObject();

                foreach (var prop in props)
                {
                    dict[prop.Name] = ParseJsonValue(prop.Value);
                }
                result.Add(dict);
            }

            return (result, null);
        }

        /// <summary>
        /// Serves JSON data for a named data source.
        /// Referenced by Data/api-connections.json connection strings and called by DevExpress
        /// server-side infrastructure when rendering reports in both the designer and viewer.
        /// 
        /// When IsSchemaCall=true, returns an empty array with proper schema structure (no data).
        /// This is used by the ReportDesigner to load column metadata without fetching actual data.
        /// When IsSchemaCall=false or not specified, returns actual data (optionally filtered by columns).
        /// </summary>
        [HttpGet]
        [Route("data")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public async Task<IActionResult> GetData(
            [FromQuery] string dataSourceName,
            [FromQuery] string[] columns,
            [FromQuery] bool isSchemaCall = false)
        {
            if (string.IsNullOrWhiteSpace(dataSourceName))
                return BadRequest("dataSourceName is required.");

            // When IsSchemaCall=true, return schema structure without actual data
            if (isSchemaCall)
            {
                var schemaResult = await GetSchemaAsEmptyDataAsync(dataSourceName);
                if (schemaResult.error != null)
                    return NotFound(schemaResult.error);
                return Ok(schemaResult.data);
            }

            var (data, error) = await GetDataFromSourceAsync(dataSourceName, columns);
            if (error != null)
                return NotFound(error);

            return Ok(data);
        }

        /// <summary>
        /// Returns schema structure as an empty array with one sample object containing null/default values.
        /// This allows DevExpress to understand the column structure without loading actual data.
        /// </summary>
        private async Task<(List<Dictionary<string, object?>>? data, string? error)> GetSchemaAsEmptyDataAsync(string dataSourceName)
        {
            var validationError = ValidateMetadataFileExists();
            if (validationError != null)
                return (null, $"Columns metadata file not found: {ColumnsMetadataPath}");

            using var stream = System.IO.File.OpenRead(ColumnsMetadataPath);
            var metadataDoc = await JsonDocument.ParseAsync(stream);

            // Find the matching data source (case-insensitive)
            foreach (var prop in metadataDoc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, dataSourceName, StringComparison.OrdinalIgnoreCase))
                {
                    var columns = ParseColumnMetadata(prop.Value);
                    
                    // Create a single sample row with default values to establish schema
                    var sampleRow = new Dictionary<string, object?>();
                    foreach (var col in columns)
                    {
                        sampleRow[col.Name] = GetDefaultValueForType(col.Type);
                    }

                    // Return a single sample row with default values so DevExpress can infer the data types
                    return (new List<Dictionary<string, object?>> { sampleRow }, null);
                }
            }

            return (null, $"Schema not found for data source '{dataSourceName}'.");
        }

        /// <summary>
        /// Returns a default value for a given column type to help DevExpress infer the schema.
        /// </summary>
        private static object? GetDefaultValueForType(string type)
        {
            return type.ToLowerInvariant() switch
            {
                "int" or "integer" => 0,
                "decimal" or "double" or "float" => 0.0m,
                "bool" or "boolean" => false,
                "date" or "datetime" => DateTime.MinValue.ToString("yyyy-MM-dd"),
                "string" or _ => string.Empty
            };
        }

        /// <summary>
        /// Lists all available data sources with their schemas (column metadata only, no data).
        /// </summary>
        [HttpGet]
        [Route("data/sources")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public async Task<IActionResult> GetDataSources()
        {
            var validationError = ValidateMetadataFileExists();
            if (validationError != null)
                return validationError;

            using var stream = System.IO.File.OpenRead(ColumnsMetadataPath);
            var metadataDoc = await JsonDocument.ParseAsync(stream);

            var dataSources = new List<DataSourceSchema>();

            foreach (var prop in metadataDoc.RootElement.EnumerateObject())
            {
                var schema = new DataSourceSchema
                {
                    Name = prop.Name,
                    Columns = ParseColumnMetadata(prop.Value)
                };

                dataSources.Add(schema);
            }

            return Ok(new DataSourcesListResponse { DataSources = dataSources });
        }

        /// <summary>
        /// Returns the schema (column names and types) for a specific data source without loading the actual data.
        /// </summary>
        [HttpGet]
        [Route("data/schema")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public async Task<IActionResult> GetSchema([FromQuery] string dataSourceName)
        {
            if (string.IsNullOrWhiteSpace(dataSourceName))
                return BadRequest("dataSourceName is required.");

            var validationError = ValidateMetadataFileExists();
            if (validationError != null)
                return validationError;

            using var stream = System.IO.File.OpenRead(ColumnsMetadataPath);
            var metadataDoc = await JsonDocument.ParseAsync(stream);

            // Find the matching data source (case-insensitive, consistent with GetDataFilePath)
            foreach (var prop in metadataDoc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, dataSourceName, StringComparison.OrdinalIgnoreCase))
                {
                    var schema = new DataSourceSchema
                    {
                        Name = prop.Name,
                        Columns = ParseColumnMetadata(prop.Value)
                    };

                    return Ok(schema);
                }
            }

            return NotFound($"Schema not found for data source '{dataSourceName}'.");
        }

        /// <summary>
        /// Fetches data from multiple data sources with column selection.
        /// This is a POST endpoint that accepts a request body specifying which data sources
        /// and which columns to retrieve.
        /// </summary>
        /// <example>
        /// Request body example:
        /// {
        ///   "sources": [
        ///     { "dataSourceName": "Pupil", "columns": ["PupilId", "FirstName"] },
        ///     { "dataSourceName": "Staff", "columns": ["StaffId", "Role"] }
        ///   ]
        /// }
        /// </example>
        [HttpPost]
        [Route("data/multi")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public async Task<IActionResult> GetMultiSourceData([FromBody] MultiSourceDataRequest request)
        {
            if (request.Sources == null || request.Sources.Count == 0)
                return BadRequest("At least one data source must be specified.");

            var response = new MultiSourceDataResponse();

            foreach (var source in request.Sources)
            {
                if (string.IsNullOrWhiteSpace(source.DataSourceName))
                {
                    response.Results.Add(new DataSourceResult
                    {
                        DataSourceName = source.DataSourceName ?? string.Empty,
                        Error = "Data source name is required."
                    });
                    continue;
                }

                var (data, error) = await GetDataFromSourceAsync(source.DataSourceName, source.Columns);
                
                response.Results.Add(new DataSourceResult
                {
                    DataSourceName = source.DataSourceName,
                    Data = data ?? new List<Dictionary<string, object?>>(),
                    Error = error
                });
            }

            return Ok(response);
        }
    }
}
