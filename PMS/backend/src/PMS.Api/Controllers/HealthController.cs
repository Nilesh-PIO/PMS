using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Abstractions;
using PMS.Application.Dtos.Health;

namespace PMS.Api.Controllers;

/// <summary>
/// F-1 health endpoints. Anonymous by design (planning-pms-verification.md, F-1 point 3, and
/// section 7: every /api/* route except health and auth/login requires the cookie), so the
/// responses must not disclose anything about the deployment beyond up/down.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    // Controllers depend on PMS.Application service interfaces and never on PmsDbContext
    // (section 2, API shape).
    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    /// <summary>Liveness: the API process is running and able to answer.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(_healthService.CheckApi());

    /// <summary>
    /// Readiness: the configured SQL Server database is reachable.
    /// 200 when it answers, 503 when it is unreachable or not configured.
    /// </summary>
    [HttpGet("db")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> GetDatabase(CancellationToken cancellationToken)
    {
        var result = await _healthService.CheckDatabaseAsync(cancellationToken);

        return result.Status == HealthResponse.Healthy
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }
}
