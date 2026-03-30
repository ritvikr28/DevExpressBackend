import { isAuthenticated } from '../services/authService';

interface ProtectedRouteProps {
    children: React.ReactNode;
}

/**
 * Protected Route component that requires authentication.
 * If user is not authenticated (no valid token), they will see an access denied message.
 * 
 * Note: Authentication tokens are expected to come from an external STS provider
 * (e.g., https://simsid-partner-stsserver.azurewebsites.net/).
 * Use setToken() from authService to store the token after obtaining it.
 */
export default function ProtectedRoute({ children }: ProtectedRouteProps) {
    // Check if user is authenticated
    if (!isAuthenticated()) {
        // Show access denied message - user needs to obtain token from external provider
        return (
            <div style={{ 
                display: 'flex', 
                justifyContent: 'center', 
                alignItems: 'center', 
                height: '100vh',
                flexDirection: 'column',
                gap: '16px',
                padding: '20px',
                textAlign: 'center'
            }}>
                <h1>Authentication Required</h1>
                <p>You need a valid authentication token to access this application.</p>
                <div style={{ 
                    backgroundColor: '#f5f5f5', 
                    padding: '20px', 
                    borderRadius: '8px',
                    maxWidth: '500px'
                }}>
                    <p style={{ color: '#666', fontSize: '14px', marginBottom: '10px' }}>
                        <strong>For Developers:</strong>
                    </p>
                    <p style={{ color: '#666', fontSize: '14px' }}>
                        1. Obtain a JWT token from the identity provider<br/>
                        2. Open browser console (F12)<br/>
                        3. Run: <code style={{ backgroundColor: '#e0e0e0', padding: '2px 6px' }}>
                            localStorage.setItem('auth_token', 'your-token-here')
                        </code><br/>
                        4. Refresh the page
                    </p>
                </div>
            </div>
        );
    }

    return <>{children}</>;
}
