import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
    getDataSources,
    getMultiSourceData,
    DataSourceSchema,
    DataSourcesListResponse,
    MultiSourceDataResponse,
    DataSourceRequest
} from '../../services/dataService';
import './DataSourceSelector.css';

interface ColumnSelection {
    [dataSourceName: string]: Set<string>;
}

interface DataPreview {
    dataSourceName: string;
    data: Record<string, unknown>[];
    error?: string;
}

export default function DataSourceSelector() {
    const navigate = useNavigate();
    const [dataSources, setDataSources] = useState<DataSourceSchema[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const [columnSelections, setColumnSelections] = useState<ColumnSelection>({});
    const [previewing, setPreviewing] = useState<boolean>(false);
    const [previewData, setPreviewData] = useState<DataPreview[] | null>(null);
    const [expandedSources, setExpandedSources] = useState<Set<string>>(new Set());

    // Load data sources on mount
    useEffect(() => {
        loadDataSources();
    }, []);

    const loadDataSources = async () => {
        try {
            setLoading(true);
            setError(null);
            const response: DataSourcesListResponse = await getDataSources();
            setDataSources(response.dataSources);
            
            // Initialize empty column selections for each data source
            const initialSelections: ColumnSelection = {};
            response.dataSources.forEach(ds => {
                initialSelections[ds.name] = new Set();
            });
            setColumnSelections(initialSelections);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load data sources');
        } finally {
            setLoading(false);
        }
    };

    const toggleSource = useCallback((sourceName: string) => {
        setExpandedSources(prev => {
            const newSet = new Set(prev);
            if (newSet.has(sourceName)) {
                newSet.delete(sourceName);
            } else {
                newSet.add(sourceName);
            }
            return newSet;
        });
    }, []);

    const toggleColumn = useCallback((dataSourceName: string, columnName: string) => {
        setColumnSelections(prev => {
            const newSelections = { ...prev };
            const currentSet = new Set(prev[dataSourceName] || []);
            
            if (currentSet.has(columnName)) {
                currentSet.delete(columnName);
            } else {
                currentSet.add(columnName);
            }
            
            newSelections[dataSourceName] = currentSet;
            return newSelections;
        });
    }, []);

    const selectAllColumns = useCallback((dataSourceName: string) => {
        const source = dataSources.find(ds => ds.name === dataSourceName);
        if (!source) return;

        setColumnSelections(prev => {
            const newSelections = { ...prev };
            newSelections[dataSourceName] = new Set(source.columns.map(c => c.name));
            return newSelections;
        });
    }, [dataSources]);

    const clearAllColumns = useCallback((dataSourceName: string) => {
        setColumnSelections(prev => {
            const newSelections = { ...prev };
            newSelections[dataSourceName] = new Set();
            return newSelections;
        });
    }, []);

    const getSelectedSourcesAndColumns = (): DataSourceRequest[] => {
        const sources: DataSourceRequest[] = [];
        
        for (const [dataSourceName, columns] of Object.entries(columnSelections)) {
            if (columns.size > 0) {
                sources.push({
                    dataSourceName,
                    columns: Array.from(columns)
                });
            }
        }

        return sources;
    };

    const hasAnySelection = (): boolean => {
        return Object.values(columnSelections).some(cols => cols.size > 0);
    };

    /**
     * Gets list of data source names that have at least one column selected
     */
    const getSelectedDataSourceNames = (): string[] => {
        return Object.entries(columnSelections)
            .filter(([, columns]) => columns.size > 0)
            .map(([name]) => name);
    };

    /**
     * Opens the Report Designer with the selected data sources.
     * DevExpress will natively fetch data from each selected source when Preview is clicked.
     */
    const handleOpenInReportDesigner = () => {
        const selectedSources = getSelectedDataSourceNames();
        
        if (selectedSources.length === 0) {
            setError('Please select at least one column from at least one data source');
            return;
        }

        // Pass selected data sources as query parameter and use MultiSourceReport which has all data sources configured
        const params = new URLSearchParams();
        params.set('dataSources', selectedSources.join(','));
        params.set('reportUrl', 'MultiSourceReport'); // Use the multi-source report template
        
        navigate(`/ReportDesigner?${params.toString()}`);
    };

    const handlePreview = async () => {
        const sources = getSelectedSourcesAndColumns();
        
        if (sources.length === 0) {
            setError('Please select at least one column from at least one data source');
            return;
        }

        try {
            setPreviewing(true);
            setError(null);
            setPreviewData(null);

            const response: MultiSourceDataResponse = await getMultiSourceData({ sources });
            
            setPreviewData(response.results.map(result => ({
                dataSourceName: result.dataSourceName,
                data: result.data,
                error: result.error
            })));
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to preview data');
        } finally {
            setPreviewing(false);
        }
    };

    const renderColumnCheckbox = (dataSourceName: string, column: { name: string; type: string }) => {
        const isSelected = columnSelections[dataSourceName]?.has(column.name) || false;
        
        return (
            <label key={column.name} className="column-checkbox">
                <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={() => toggleColumn(dataSourceName, column.name)}
                />
                <span className="column-name">{column.name}</span>
                <span className="column-type">({column.type})</span>
            </label>
        );
    };

    const renderDataSource = (source: DataSourceSchema) => {
        const isExpanded = expandedSources.has(source.name);
        const selectedCount = columnSelections[source.name]?.size || 0;
        
        return (
            <div key={source.name} className="data-source-card">
                <div className="data-source-header" onClick={() => toggleSource(source.name)}>
                    <span className={`expand-icon ${isExpanded ? 'expanded' : ''}`}>▶</span>
                    <span className="data-source-name">{source.name}</span>
                    <span className="column-count">
                        {selectedCount > 0 ? `${selectedCount}/${source.columns.length} selected` : `${source.columns.length} columns`}
                    </span>
                </div>
                
                {isExpanded && (
                    <div className="data-source-columns">
                        <div className="column-actions">
                            <button 
                                type="button" 
                                className="btn btn-sm btn-outline-primary"
                                onClick={(e) => { e.stopPropagation(); selectAllColumns(source.name); }}
                            >
                                Select All
                            </button>
                            <button 
                                type="button" 
                                className="btn btn-sm btn-outline-secondary"
                                onClick={(e) => { e.stopPropagation(); clearAllColumns(source.name); }}
                            >
                                Clear All
                            </button>
                        </div>
                        <div className="columns-list">
                            {source.columns.map(col => renderColumnCheckbox(source.name, col))}
                        </div>
                    </div>
                )}
            </div>
        );
    };

    const renderPreviewTable = (preview: DataPreview) => {
        if (preview.error) {
            return (
                <div key={preview.dataSourceName} className="preview-error">
                    <h5>{preview.dataSourceName}</h5>
                    <div className="alert alert-danger">{preview.error}</div>
                </div>
            );
        }

        if (preview.data.length === 0) {
            return (
                <div key={preview.dataSourceName} className="preview-empty">
                    <h5>{preview.dataSourceName}</h5>
                    <p className="text-muted">No data available</p>
                </div>
            );
        }

        const columns = Object.keys(preview.data[0]);

        return (
            <div key={preview.dataSourceName} className="preview-table-container">
                <h5>{preview.dataSourceName}</h5>
                <div className="table-responsive">
                    <table className="table table-striped table-bordered">
                        <thead>
                            <tr>
                                {columns.map(col => (
                                    <th key={col}>{col}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {preview.data.map((row, idx) => (
                                <tr key={idx}>
                                    {columns.map(col => (
                                        <td key={col}>{String(row[col] ?? '')}</td>
                                    ))}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>
        );
    };

    if (loading) {
        return (
            <div className="data-source-selector">
                <div className="loading">Loading data sources...</div>
            </div>
        );
    }

    return (
        <div className="data-source-selector container-fluid">
            <h2>Data Source Selector</h2>
            <p className="lead">
                Select data sources and columns, then click Preview to see the data, or Open in Report Designer to design a report with the selected sources.
            </p>

            {error && (
                <div className="alert alert-danger" role="alert">
                    {error}
                    <button 
                        type="button" 
                        className="btn-close" 
                        onClick={() => setError(null)}
                        aria-label="Close"
                    ></button>
                </div>
            )}

            <div className="row">
                <div className="col-md-6">
                    <div className="card">
                        <div className="card-header">
                            <h4>Available Data Sources</h4>
                            <button 
                                type="button" 
                                className="btn btn-link btn-sm"
                                onClick={loadDataSources}
                            >
                                Refresh
                            </button>
                        </div>
                        <div className="card-body">
                            {dataSources.length === 0 ? (
                                <p className="text-muted">No data sources available</p>
                            ) : (
                                <div className="data-sources-list">
                                    {dataSources.map(source => renderDataSource(source))}
                                </div>
                            )}
                        </div>
                        <div className="card-footer">
                            <div className="d-flex flex-wrap gap-2 align-items-center">
                                <button
                                    type="button"
                                    className="btn btn-primary"
                                    onClick={handlePreview}
                                    disabled={previewing || !hasAnySelection()}
                                >
                                    {previewing ? 'Loading...' : 'Preview Data'}
                                </button>
                                
                                <button
                                    type="button"
                                    className="btn btn-success"
                                    onClick={handleOpenInReportDesigner}
                                    disabled={!hasAnySelection()}
                                    title="Open selected data sources in Report Designer. DevExpress will fetch data from each source when you click Preview."
                                >
                                    Open in Report Designer
                                </button>
                                
                                {!hasAnySelection() && (
                                    <span className="text-muted ms-2">
                                        Select columns to enable actions
                                    </span>
                                )}
                            </div>
                        </div>
                    </div>
                </div>

                <div className="col-md-6">
                    <div className="card">
                        <div className="card-header">
                            <h4>Data Preview</h4>
                        </div>
                        <div className="card-body preview-area">
                            {previewing && (
                                <div className="loading">Loading preview data...</div>
                            )}
                            
                            {!previewing && !previewData && (
                                <p className="text-muted">
                                    Select columns from data sources and click "Preview Data" to see the results.
                                </p>
                            )}
                            
                            {!previewing && previewData && (
                                <div className="preview-results">
                                    {previewData.map(preview => renderPreviewTable(preview))}
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            <div className="selected-summary mt-3">
                <h5>Selected Configuration</h5>
                <pre className="bg-light p-3 rounded">
                    {JSON.stringify(getSelectedSourcesAndColumns(), null, 2)}
                </pre>
            </div>
        </div>
    );
}
