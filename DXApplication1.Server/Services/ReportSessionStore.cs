using System;
using System.Collections.Concurrent;

namespace DXApplication1.Services
{
    // Singleton: holds short-lived report ID sessions created via POST /api/reports/session.
    // Avoids encoding large ID sets in the report URL.
    public class ReportSessionStore
    {
        private readonly ConcurrentDictionary<string, int[]> _sessions = new();

        // Stores the IDs and returns a 32-char hex token (URL-safe, no hyphens).
        public string Create(int[] ids)
        {
            var token = Guid.NewGuid().ToString("N");
            _sessions[token] = ids;
            return token;
        }

        // Returns the IDs for a token, or null if not found.
        public int[]? Get(string token) =>
            _sessions.TryGetValue(token, out var ids) ? ids : null;
    }
}
