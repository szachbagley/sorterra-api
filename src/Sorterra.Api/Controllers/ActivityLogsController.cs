using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Core.DTOs;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var entities = await _dbContext.ActivityLogs.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.ActivityLogs.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateActivityLogDto dto)
    {
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
        var entity = await _dbContext.ActivityLogs.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.Description is not null) entity.Description = dto.Description;
        if (dto.Metadata is not null) entity.Metadata = dto.Metadata;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.ActivityLogs.FindAsync(id);
        if (entity == null) return NotFound();

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
