#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DXApplication1.Server.Models
{
    /// <summary>
    /// Represents column metadata for a data source.
    /// </summary>
    public class ColumnMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Represents a data source with its available columns (schema only, no data).
    /// </summary>
    public class DataSourceSchema
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("columns")]
        public List<ColumnMetadata> Columns { get; set; } = new();
    }
    
    /// <summary>
    /// Request to fetch data from a specific source with optional column selection.
    /// </summary>
    public class DataSourceRequest
    {
        [JsonPropertyName("dataSourceName")]
        public string DataSourceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional list of columns to retrieve. If empty or null, all columns are returned.
        /// </summary>
        [JsonPropertyName("columns")]
        public List<string>? Columns { get; set; }
    }
    
    /// <summary>
    /// Request to fetch data from multiple data sources.
    /// </summary>
    public class MultiSourceDataRequest
    {
        [JsonPropertyName("sources")]
        public List<DataSourceRequest> Sources { get; set; } = new();
    }
    
    /// <summary>
    /// Response containing data from a single source.
    /// </summary>
    public class DataSourceResult
    {
        [JsonPropertyName("dataSourceName")]
        public string DataSourceName { get; set; } = string.Empty;
        
        [JsonPropertyName("data")]
        public List<Dictionary<string, object?>> Data { get; set; } = new();
        
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
    
    /// <summary>
    /// Response containing data from multiple sources.
    /// </summary>
    public class MultiSourceDataResponse
    {
        [JsonPropertyName("results")]
        public List<DataSourceResult> Results { get; set; } = new();
    }
    
    /// <summary>
    /// List of available data sources.
    /// </summary>
    public class DataSourcesListResponse
    {
        [JsonPropertyName("dataSources")]
        public List<DataSourceSchema> DataSources { get; set; } = new();
    }
}
