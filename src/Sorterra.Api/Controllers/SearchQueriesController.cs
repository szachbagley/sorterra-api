using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Core.DTOs;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchQueriesController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<SearchQueriesController> _logger;

    public SearchQueriesController(SorterraDbContext dbContext, ILogger<SearchQueriesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.SearchQueries.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.SearchQueries.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSearchQueryDto dto)
    {
        var entity = new SearchQuery
        {
            Id = Guid.NewGuid(),
            OrganizationId = dto.OrganizationId,
            UserId = dto.UserId,
            QueryText = dto.QueryText,
            QueryEmbedding = dto.QueryEmbedding,
            ResultsCount = dto.ResultsCount,
            LatencyMs = dto.LatencyMs,
            ClickedResultIds = dto.ClickedResultIds,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.SearchQueries.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateSearchQueryDto dto)
    {
        var entity = await _dbContext.SearchQueries.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.ResultsCount is not null) entity.ResultsCount = dto.ResultsCount;
        if (dto.LatencyMs is not null) entity.LatencyMs = dto.LatencyMs;
        if (dto.ClickedResultIds is not null) entity.ClickedResultIds = dto.ClickedResultIds;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.SearchQueries.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.SearchQueries.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static SearchQueryResponseDto MapToResponse(SearchQuery entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.UserId,
        entity.QueryText,
        entity.QueryEmbedding,
        entity.ResultsCount,
        entity.LatencyMs,
        entity.ClickedResultIds,
        entity.CreatedAt
    );
}
