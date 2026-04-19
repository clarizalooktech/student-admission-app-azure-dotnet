using Microsoft.AspNetCore.Mvc;
using AdmissionAgent.Models;
using AdmissionAgent.Services;

namespace AdmissionAgent.Controllers;

[ApiController]
[Route("api/admission")]
public class AdmissionController : ControllerBase
{
    private readonly AdmissionAgentService _agent;
    private readonly ILogger<AdmissionController> _logger;

    public AdmissionController(AdmissionAgentService agent, ILogger<AdmissionController> logger)
    {
        _agent  = agent;
        _logger = logger;
    }

    // POST /api/admission/evaluate
    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] ApplicationForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Email))
            return BadRequest("Email is required.");

        _logger.LogInformation("Received application from {Email}", form.Email);
        var result = await _agent.EvaluateAsync(form);

        // Serialize outcome as lowercase string for React
        return Ok(new {
            status   = result.Status,
            steps    = result.Steps.Select(s => new {
                label  = s.Label,
                status = s.Status.ToString().ToLower()
            }),
            decision = new {
                outcome = result.Decision.Outcome.ToString().ToLower(),
                summary = result.Decision.Summary,
            },
            traceId = result.TraceId,
        });
    }

    // GET /api/admission/health
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
