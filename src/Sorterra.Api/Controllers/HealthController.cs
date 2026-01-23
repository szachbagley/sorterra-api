using Microsoft.AspNetCore.Mvc;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(SorterraDbContext dbContext, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var health = new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Checks = new Dictionary<string, string>()
        };

        // Check database connectivity
        try
        {
            await _dbContext.Database.CanConnectAsync();
            health.Checks["database"] = "Healthy";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            health.Checks["database"] = "Unhealthy";
            health.Status = "Unhealthy";
        }

        return health.Status == "Healthy"
            ? Ok(health)
            : StatusCode(503, health);
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        // Readiness check - is the app ready to receive traffic?
        try
        {
            await _dbContext.Database.CanConnectAsync();
            return Ok(new { status = "Ready" });
        }
        catch
        {
            return StatusCode(503, new { status = "Not Ready" });
        }
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        // Liveness check - is the app running?
        return Ok(new { status = "Alive" });
    }
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Checks { get; set; } = new();
}
