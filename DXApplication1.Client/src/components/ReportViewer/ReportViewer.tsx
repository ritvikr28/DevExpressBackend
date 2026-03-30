import { useCallback } from 'react';
import DxReportViewer, { Callbacks, ProgressBarSettings, RequestOptions } from 'devexpress-reporting-react/dx-report-viewer';
import { fetchSetup } from '@devexpress/analytics-core/analytics-utils';
import { useSearchParams } from 'react-router-dom';
import { getToken } from '../../services/authService';
import './ReportViewer.css';

export default function ReportViewer(props: { hostUrl: string }) {
    const [searchParams] = useSearchParams();
    const reportUrl: string = searchParams.get('reportUrl') ?? 'TestReport';
    const invokeAction: string = '/DXXRDV';
    const getLocalizationAction: string = `${invokeAction}/GetLocalization`;

    // BeforeRender fires before the DevExpress viewer makes any HTTP requests,
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
        <DxReportViewer reportUrl={reportUrl} height="calc(100vh - 90px)" developmentMode={true}>
            <RequestOptions invokeAction={invokeAction} getLocalizationAction={getLocalizationAction} host={props.hostUrl} />
            <Callbacks BeforeRender={onBeforeRender} />
            <ProgressBarSettings position="BottomLeft"></ProgressBarSettings>
        </DxReportViewer>
    );
}
