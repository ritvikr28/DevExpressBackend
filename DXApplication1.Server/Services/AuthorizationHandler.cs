using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication1.Services
{
    /// <summary>
    /// DelegatingHandler that extracts the bearer token from the incoming HTTP request
    /// and attaches it to outgoing HttpClient requests. This enables transparent
    /// token forwarding to downstream APIs (e.g., learners API).
    /// </summary>
    public class AuthorizationHandler : DelegatingHandler
    {
        private const string AuthenticationHeaderScheme = "Bearer";
        private const string TokenName = "access_token";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthorizationHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                // First, try to get the token from the authentication system (e.g., cookie auth, OpenID Connect)
                var token = await httpContext.GetTokenAsync(TokenName).ConfigureAwait(false);

                // If not found, fall back to reading the Authorization header directly
                if (string.IsNullOrEmpty(token))
                {
                    var authHeader = httpContext.Request.Headers[HeaderNames.Authorization].ToString();
                    if (!string.IsNullOrEmpty(authHeader) &&
                        authHeader.StartsWith($"{AuthenticationHeaderScheme} ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = authHeader.Substring(AuthenticationHeaderScheme.Length + 1).Trim();
                    }
                }

                // Attach the token to the outgoing request if available
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationHeaderScheme, token);
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
