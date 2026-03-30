import { useCallback, useMemo } from 'react';
import DxReportDesigner, { Callbacks, DesignerModelSettings, PreviewSettings, RequestOptions } from "devexpress-reporting-react/dx-report-designer";
import { SearchSettings } from 'devexpress-reporting-react/dx-report-viewer';
import { fetchSetup } from '@devexpress/analytics-core/analytics-utils';
import { useSearchParams, Link } from 'react-router-dom';
import { getToken } from '../../services/authService';
import './ReportDesigner.css';

// Height constants for the designer component
const NAVBAR_HEIGHT = 90; // Main navbar height in pixels
const BANNER_HEIGHT = 50; // Data sources banner height in pixels

export default function ReportDesigner(props: { hostUrl: string }) {
    const [searchParams] = useSearchParams();
    const getDesignerModelAction: string = 'DXXRD/GetDesignerModel';
    const getLocalizationAction: string = 'DXXRD/GetLocalization';
    const reportUrl: string = searchParams.get('reportUrl') ?? 'TestReport';
    
    // Parse selected data sources from query params (passed from DataSourceSelector)
    const selectedDataSources = useMemo(() => {
        const sourcesParam = searchParams.get('dataSources');
        return sourcesParam ? sourcesParam.split(',').filter(s => s.trim()) : [];
    }, [searchParams]);
    
    // Calculate designer height based on whether banner is shown
    const designerHeight = selectedDataSources.length > 0 
        ? `calc(100vh - ${NAVBAR_HEIGHT + BANNER_HEIGHT}px)` 
        : `calc(100vh - ${NAVBAR_HEIGHT}px)`;
    
    // BeforeRender fires before the DevExpress designer makes any HTTP requests,
    // so this is the correct place to ensure the Authorization header is set.
    const onBeforeRender = useCallback(() => {
        const token = getToken();
        if (token) {
            fetchSetup.fetchSettings = {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            };
        }
    }, []);
    
    return (
        <div className="report-designer-container">
            {/* Show selected data sources banner if coming from DataSourceSelector */}
            {selectedDataSources.length > 0 && (
                <div className="selected-sources-banner alert alert-info mb-0">
                    <strong>Selected Data Sources:</strong>{' '}
                    {selectedDataSources.map((source) => (
                        <span key={source} className="badge bg-primary me-1">
                            {source}
                        </span>
                    ))}
                    <span className="ms-2 text-muted">
                        — These data sources are available in the Field List. Add them to your report, then click Preview to fetch data from all sources.
                    </span>
                    <Link to="/DataSourceSelector" className="btn btn-sm btn-outline-secondary ms-2">
                        Change Selection
                    </Link>
                </div>
            )}
            
            <DxReportDesigner reportUrl={reportUrl} height={designerHeight} developmentMode={true}>
                <RequestOptions host={props.hostUrl} getLocalizationAction={getLocalizationAction} getDesignerModelAction={getDesignerModelAction} />
                <Callbacks BeforeRender={onBeforeRender}></Callbacks>
                <DesignerModelSettings>
                    <PreviewSettings>
                        <SearchSettings searchEnabled={true}></SearchSettings>
                    </PreviewSettings>
                </DesignerModelSettings>
            </DxReportDesigner>
        </div>
    );
}
