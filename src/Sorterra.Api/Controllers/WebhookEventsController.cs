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
public class WebhookEventsController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<WebhookEventsController> _logger;

    public WebhookEventsController(SorterraDbContext dbContext, ILogger<WebhookEventsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.WebhookEvents.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.WebhookEvents.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWebhookEventDto dto)
    {
        var entity = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            ConnectionId = dto.ConnectionId,
            EventType = dto.EventType,
            ResourceType = dto.ResourceType,
            ResourceId = dto.ResourceId,
            RawPayload = dto.RawPayload,
            ProcessingStatus = dto.ProcessingStatus ?? "received",
            ReceivedAt = DateTime.UtcNow
        };

        _dbContext.WebhookEvents.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateWebhookEventDto dto)
    {
        var entity = await _dbContext.WebhookEvents.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.ProcessingStatus is not null) entity.ProcessingStatus = dto.ProcessingStatus;
        if (dto.ProcessedAt is not null) entity.ProcessedAt = dto.ProcessedAt;
        if (dto.ErrorMessage is not null) entity.ErrorMessage = dto.ErrorMessage;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.WebhookEvents.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.WebhookEvents.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static WebhookEventResponseDto MapToResponse(WebhookEvent entity) => new(
        entity.Id,
        entity.ConnectionId,
        entity.EventType,
        entity.ResourceType,
        entity.ResourceId,
        entity.RawPayload,
        entity.ProcessingStatus,
        entity.ProcessedAt,
        entity.ErrorMessage,
        entity.ReceivedAt
    );
}
