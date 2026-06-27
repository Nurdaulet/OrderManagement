using Microsoft.AspNetCore.Mvc;

namespace OrderManagement.Api.Controllers;

/// <summary>
/// Liveness endpoint used to verify the API host is up and responding.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns the current health status of the service.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() =>
        Ok(new HealthResponse("Healthy", DateTimeOffset.UtcNow));
}

/// <summary>Health check response payload.</summary>
/// <param name="Status">Service status indicator.</param>
/// <param name="TimestampUtc">Time the response was produced (UTC).</param>
public sealed record HealthResponse(string Status, DateTimeOffset TimestampUtc);
