using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Api.Services;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[ApiController]
[Route("auth/sharepoint")]
public class SharePointAuthController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ConsentStateService _stateService;
    private readonly IConfiguration _config;
    private readonly ILogger<SharePointAuthController> _logger;

    public SharePointAuthController(
        SorterraDbContext dbContext,
        ConsentStateService stateService,
        IConfiguration config,
        ILogger<SharePointAuthController> logger)
    {
        _dbContext = dbContext;
        _stateService = stateService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Returns the Azure AD admin consent URL for a pending SharePoint connection.
    /// The frontend should redirect the browser to this URL.
    /// </summary>
    [Authorize]
    [HttpGet("consent")]
    public async Task<IActionResult> GetConsentUrl([FromQuery] Guid connectionId)
    {
        var connection = await _dbContext.SharePointConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId);

        if (connection == null)
            return NotFound(new { error = "Connection not found" });

        if (connection.ConnectionStatus != "pending")
            return BadRequest(new { error = $"Connection is already in '{connection.ConnectionStatus}' status" });

        var clientId = _config["AzureAd:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return StatusCode(500, new { error = "AzureAd:ClientId is not configured" });

        var redirectUri = _config["AzureAd:ConsentRedirectUri"];
        if (string.IsNullOrEmpty(redirectUri))
            return StatusCode(500, new { error = "AzureAd:ConsentRedirectUri is not configured" });

        var state = _stateService.CreateState(connectionId);

        var consentUrl = $"https://login.microsoftonline.com/common/adminconsent"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

        _logger.LogInformation("Generated consent URL for connection {ConnectionId}", connectionId);

        return Ok(new { consentUrl });
    }

    /// <summary>
    /// Callback endpoint that Microsoft redirects to after admin consent.
    /// Validates the state, captures the tenant ID, and redirects to the frontend.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state,
        [FromQuery] string? tenant,
        [FromQuery] string? admin_consent,
        [FromQuery] string? error,
        [FromQuery] string? error_description)
    {
        var frontendBase = _config["FrontendBaseUrl"] ?? "http://localhost:3000";

        // Handle errors from Microsoft
        if (!string.IsNullOrEmpty(error))
        {
            var message = error_description ?? error;
            _logger.LogWarning("Admin consent failed: {Error} - {Description}", error, error_description);
            return Redirect($"{frontendBase}/settings?consent=error&message={Uri.EscapeDataString(message)}");
        }

        // Validate state JWT
        if (string.IsNullOrEmpty(state))
        {
            return Redirect($"{frontendBase}/settings?consent=error&message={Uri.EscapeDataString("Missing state parameter")}");
        }

        var (connectionId, isValid) = _stateService.ValidateState(state);
        if (!isValid)
        {
            _logger.LogWarning("Invalid or expired state token in consent callback");
            return Redirect($"{frontendBase}/settings?consent=error&message={Uri.EscapeDataString("Invalid or expired authorization state. Please try again.")}");
        }

        // Check consent was granted
        if (admin_consent != "True")
        {
            return Redirect($"{frontendBase}/settings?consent=error&message={Uri.EscapeDataString("Admin consent was not granted.")}");
        }

        if (string.IsNullOrEmpty(tenant))
        {
            return Redirect($"{frontendBase}/settings?consent=error&message={Uri.EscapeDataString("No tenant ID received from Microsoft.")}");
        }

        // Update the connection
        var connection = await _dbContext.SharePointConnections.FindAsync(connectionId);
        if (connection == null)
        {
            return Redirect($"{frontendBase}/settings?consent=error&message={Uri.EscapeDataString("Connection not found.")}");
        }

        connection.TenantId = tenant;
        connection.ConnectionStatus = "consented";
        connection.UpdatedAt = DateTime.UtcNow;

        _dbContext.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = connection.OrganizationId,
            ActivityType = "admin_consent_granted",
            EntityType = "SharePointConnection",
            EntityId = connection.Id,
            Description = $"Admin consent granted for {connection.SiteUrl}",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Admin consent granted for connection {ConnectionId}, tenant {TenantId}",
            connectionId, tenant);

        return Redirect($"{frontendBase}/settings?consent=success&connectionId={connectionId}");
    }
}
