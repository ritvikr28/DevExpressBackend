/**
 * Authentication service for managing JWT tokens from external STS provider
 * Tokens are obtained from: https://core-part.sims.co.uk
 */

const TOKEN_KEY = 'auth_token';

/**
 * Store authentication token in local storage
 * Call this after obtaining a token from the external STS provider
 */
export const setToken = (token: string): void => {
    localStorage.setItem(TOKEN_KEY, token);
};

/**
 * Get authentication token from local storage
 */
export const getToken = (): string | null => {
    return localStorage.getItem(TOKEN_KEY);
};

/**
 * Remove authentication token from local storage
 */
export const removeToken = (): void => {
    localStorage.removeItem(TOKEN_KEY);
};

/**
 * Check if user is authenticated (has a structurally valid JWT token).
 * Lifetime validation is intentionally skipped here — the server controls
 * whether an expired token is accepted (ValidateLifetime on the server side).
 */
export const isAuthenticated = (): boolean => {
    const token = getToken();
    if (!token) return false;
    
    // Verify basic JWT structure: must have 3 parts separated by dots
    try {
        const parts = token.split('.');
        if (parts.length !== 3) {
            return false;
        }
        
        const payloadBase64 = parts[1];
        if (!payloadBase64) {
            return false;
        }
        
        // Parse to validate the payload is well-formed JSON (result intentionally discarded)
        JSON.parse(atob(payloadBase64));
        return true;
    } catch {
        return false;
    }
};

/**
 * Logout and clear authentication data
 */
export const logout = (): void => {
    removeToken();
};

/**
 * Create headers with authorization token
 */
export const getAuthHeaders = (): HeadersInit => {
    const token = getToken();
    if (!token) return {};
    return {
        'Authorization': `Bearer ${token}`,
    };
};

/**
 * Authenticated fetch wrapper that adds authorization header
 */
export const authFetch = async (
    url: string,
    options: RequestInit = {}
): Promise<Response> => {
    const token = getToken();
    
    const headers = new Headers(options.headers);
    if (token) {
        headers.set('Authorization', `Bearer ${token}`);
    }
    
    const response = await fetch(url, {
        ...options,
        headers,
    });

    // If unauthorized, clear auth data and let the caller handle navigation
    if (response.status === 401) {
        logout();
    }

    return response;
};
