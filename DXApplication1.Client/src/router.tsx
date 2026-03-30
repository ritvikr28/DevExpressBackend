import { createBrowserRouter, createRoutesFromElements, Route } from "react-router-dom";
import Home  from './components/Home';
import ReportViewer from './components/ReportViewer';
import ReportDesigner from './components/ReportDesigner';
import DataSourceSelector from './components/DataSourceSelector';
import ProtectedRoute from './components/ProtectedRoute';
import { App } from './App';

function getBaseUrl() {
    return document.getElementsByTagName('base')[0].href;
}

export const router = createBrowserRouter(createRoutesFromElements(
    <Route path="/" element={<App />}>
        <Route path="/" element={
            <ProtectedRoute>
                <Home />
            </ProtectedRoute>
        } />
        <Route path="DocumentViewer" element={
            <ProtectedRoute>
                <ReportViewer hostUrl={getBaseUrl()} />
            </ProtectedRoute>
        } />
        <Route path="ReportDesigner" element={
            <ProtectedRoute>
                <ReportDesigner hostUrl={getBaseUrl()} />
            </ProtectedRoute>
        } />
        <Route path="DataSourceSelector" element={
            <ProtectedRoute>
                <DataSourceSelector />
            </ProtectedRoute>
        } />
    </Route>
));