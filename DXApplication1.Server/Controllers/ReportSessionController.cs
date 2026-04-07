using DXApplication1.Services;
using ESS.Platform.Authorization.Attributes;
using ESS.Platform.Authorization.Enums;
using Microsoft.AspNetCore.Mvc;

namespace DXApplication1.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportSessionController : ControllerBase
    {
        private readonly ReportSessionStore _store;

        public ReportSessionController(ReportSessionStore store)
        {
            _store = store;
        }

        // POST /api/reports/session
        // Body: [1, 2, 3]  (JSON int array of learner IDs)
        // Returns: { "token": "abc123..." }
        [HttpPost("session")]
        [SecurityDomain(["NG.Homepage.Access"], Operation.View)]
        public IActionResult CreateSession([FromBody] int[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BadRequest("At least one ID is required.");

            var token = _store.Create(ids);
            return Ok(new { token });
        }
    }
}
