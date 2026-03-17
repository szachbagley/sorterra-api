using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Core.DTOs;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class SharePointConnectionsController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<SharePointConnectionsController> _logger;

    public SharePointConnectionsController(SorterraDbContext dbContext, ILogger<SharePointConnectionsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.SharePointConnections.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.SharePointConnections.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSharePointConnectionDto dto)
    {
        if (dto.OrganizationId is null || dto.OrganizationId == Guid.Empty)
            return BadRequest(new { error = "Organization ID is required." });

        var entity = new SharePointConnection
        {
            Id = Guid.NewGuid(),
            OrganizationId = dto.OrganizationId.Value,
            SiteUrl = dto.SiteUrl,
            TenantId = dto.TenantId,
            ClientId = dto.ClientId,
            Thumbprint = dto.Thumbprint,
            PrivateKeyPath = dto.PrivateKeyPath,
            DriveId = dto.DriveId,
            SourceFolder = dto.SourceFolder,
            ConnectionStatus = dto.ConnectionStatus ?? "pending",
            CreatedBy = dto.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.SharePointConnections.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateSharePointConnectionDto dto)
    {
        var entity = await _dbContext.SharePointConnections.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.SiteUrl is not null) entity.SiteUrl = dto.SiteUrl;
        if (dto.TenantId is not null) entity.TenantId = dto.TenantId;
        if (dto.ClientId is not null) entity.ClientId = dto.ClientId;
        if (dto.Thumbprint is not null) entity.Thumbprint = dto.Thumbprint;
        if (dto.PrivateKeyPath is not null) entity.PrivateKeyPath = dto.PrivateKeyPath;
        if (dto.DriveId is not null) entity.DriveId = dto.DriveId;
        if (dto.SourceFolder is not null) entity.SourceFolder = dto.SourceFolder;
        if (dto.ConnectionStatus is not null) entity.ConnectionStatus = dto.ConnectionStatus;
        if (dto.LastSyncAt is not null) entity.LastSyncAt = dto.LastSyncAt;
        if (dto.WebhookSubscriptionId is not null) entity.WebhookSubscriptionId = dto.WebhookSubscriptionId;
        if (dto.WebhookExpiration is not null) entity.WebhookExpiration = dto.WebhookExpiration;
        if (dto.ErrorMessage is not null) entity.ErrorMessage = dto.ErrorMessage;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.SharePointConnections.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.SharePointConnections.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static SharePointConnectionResponseDto MapToResponse(SharePointConnection entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.SiteUrl,
        entity.TenantId,
        entity.ClientId,
        entity.Thumbprint,
        entity.PrivateKeyPath,
        entity.DriveId,
        entity.SourceFolder,
        entity.ConnectionStatus,
        entity.LastSyncAt,
        entity.WebhookSubscriptionId,
        entity.WebhookExpiration,
        entity.CreatedBy,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.ErrorMessage
    );
}
