import { Link, useNavigate } from 'react-router-dom';
import { useState } from 'react';
import { isAuthenticated, logout } from '../../services/authService';

export default function NavMenu() {
    const [isExpanded, setExpanded] = useState(false);
    const navigate = useNavigate();
    const authenticated = isAuthenticated();

    const toggle = () => {
        setExpanded(!isExpanded);
    }

    const handleLogout = () => {
        logout();
        navigate('/');
        // Force a refresh to show the authentication required message
        window.location.reload();
    }

    // Don't show nav menu when not authenticated
    if (!authenticated) {
        return null;
    }

    return (
        <>
            <header>
                <nav className="navbar navbar-expand-sm navbar-toggleable-sm navbar-light bg-white border-bottom box-shadow">
                    <div className="container-fluid">
                        <Link className="navbar-brand" to="/">ReactReportingApp</Link>
                        <button className="navbar-toggler"
                            type="button"
                            data-toggle="collapse"
                            data-target=".navbar-collapse"
                            aria-label="Toggle navigation"
                            aria-expanded={isExpanded}
                            onClick={toggle} >
                            <span className="navbar-toggler-icon"></span>
                        </button>
                        <div className={`navbar-collapse collapse d-sm-inline-flex flex-sm-row-reverse ${isExpanded && "show"}`}>
                            <ul className="navbar-nav flex-grow">
                                <li className="nav-item" >
                                    <Link className="nav-link text-dark" to="/">Home</Link>
                                </li>
                                <li className="nav-item">
                                    <Link className="nav-link text-dark" to="/DataSourceSelector">Data Sources</Link>
                                </li>
                                <li className="nav-item">
                                    <Link className="nav-link text-dark" to="/ReportDesigner">Report Designer</Link>
                                </li>
                                <li className="nav-item">
                                    <Link className="nav-link text-dark" to="/DocumentViewer">Document Viewer</Link>
                                </li>
                            </ul>
                            <div className="navbar-nav ms-auto">
                                <button 
                                    className="btn btn-outline-secondary btn-sm"
                                    onClick={handleLogout}
                                >
                                    Logout
                                </button>
                            </div>
                        </div>
                    </div>
                </nav>
            </header>
        </>
    );
}