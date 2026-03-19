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
public class ActivityLogsController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<ActivityLogsController> _logger;

    public ActivityLogsController(SorterraDbContext dbContext, ILogger<ActivityLogsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cognitoSub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var userOrgs = await _dbContext.UserOrganizations
            .Where(uo => uo.User.CognitoSub == cognitoSub)
            .Select(uo => uo.OrganizationId)
            .ToListAsync();

        var entities = await _dbContext.ActivityLogs
            .Where(e => userOrgs.Contains(e.OrganizationId))
            .ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cognitoSub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var userOrgs = await _dbContext.UserOrganizations
            .Where(uo => uo.User.CognitoSub == cognitoSub)
            .Select(uo => uo.OrganizationId)
            .ToListAsync();

        var entity = await _dbContext.ActivityLogs.FindAsync(id);
        if (entity == null || !userOrgs.Contains(entity.OrganizationId)) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateActivityLogDto dto)
    {
        var cognitoSub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var userOrgs = await _dbContext.UserOrganizations
            .Where(uo => uo.User.CognitoSub == cognitoSub)
            .Select(uo => uo.OrganizationId)
            .ToListAsync();

        if (!userOrgs.Contains(dto.OrganizationId)) return Forbid();

        var entity = new ActivityLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = dto.OrganizationId,
            UserId = dto.UserId,
            ActivityType = dto.ActivityType,
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            Description = dto.Description,
            Metadata = dto.Metadata ?? "{}",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ActivityLogs.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateActivityLogDto dto)
    {
        var cognitoSub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var userOrgs = await _dbContext.UserOrganizations
            .Where(uo => uo.User.CognitoSub == cognitoSub)
            .Select(uo => uo.OrganizationId)
            .ToListAsync();

        var entity = await _dbContext.ActivityLogs.FindAsync(id);
        if (entity == null || !userOrgs.Contains(entity.OrganizationId)) return NotFound();

        if (dto.Description is not null) entity.Description = dto.Description;
        if (dto.Metadata is not null) entity.Metadata = dto.Metadata;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cognitoSub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var userOrgs = await _dbContext.UserOrganizations
            .Where(uo => uo.User.CognitoSub == cognitoSub)
            .Select(uo => uo.OrganizationId)
            .ToListAsync();

        var entity = await _dbContext.ActivityLogs.FindAsync(id);
        if (entity == null || !userOrgs.Contains(entity.OrganizationId)) return NotFound();

        _dbContext.ActivityLogs.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static ActivityLogResponseDto MapToResponse(ActivityLog entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.UserId,
        entity.ActivityType,
        entity.EntityType,
        entity.EntityId,
        entity.Description,
        entity.Metadata,
        entity.CreatedAt
    );
}
