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
public class SortingRecipesController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<SortingRecipesController> _logger;

    public SortingRecipesController(SorterraDbContext dbContext, ILogger<SortingRecipesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? organizationId, [FromQuery] bool? isActive, [FromQuery] string? orderBy)
    {
        var query = _dbContext.SortingRecipes.AsQueryable();

        if (organizationId.HasValue)
            query = query.Where(r => r.OrganizationId == organizationId.Value);

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        query = orderBy?.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(r => r.Name),
            "createdat" => query.OrderByDescending(r => r.CreatedAt),
            _ => query.OrderBy(r => r.Priority)
        };

        var entities = await query.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("by-connection/{connectionId}")]
    public async Task<IActionResult> GetByConnection(Guid connectionId)
    {
        var connection = await _dbContext.SharePointConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId);

        if (connection == null)
            return NotFound(new { error = "Connection not found", connectionId });

        var recipes = await _dbContext.SortingRecipes
            .Where(r => r.OrganizationId == connection.OrganizationId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        return Ok(recipes.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.SortingRecipes.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSortingRecipeDto dto)
    {
        var entity = new SortingRecipe
        {
            Id = Guid.NewGuid(),
            OrganizationId = dto.OrganizationId,
            Name = dto.Name,
            Description = dto.Description,
            FileTypePattern = dto.FileTypePattern,
            DestinationPathTemplate = dto.DestinationPathTemplate,
            IsActive = dto.IsActive ?? true,
            Priority = dto.Priority ?? 0,
            CreatedBy = dto.CreatedBy,
            Rules = dto.Rules ?? "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.SortingRecipes.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateSortingRecipeDto dto)
    {
        var entity = await _dbContext.SortingRecipes.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.Name is not null) entity.Name = dto.Name;
        if (dto.Description is not null) entity.Description = dto.Description;
        if (dto.FileTypePattern is not null) entity.FileTypePattern = dto.FileTypePattern;
        if (dto.DestinationPathTemplate is not null) entity.DestinationPathTemplate = dto.DestinationPathTemplate;
        if (dto.IsActive is not null) entity.IsActive = dto.IsActive.Value;
        if (dto.Priority is not null) entity.Priority = dto.Priority.Value;
        if (dto.Rules is not null) entity.Rules = dto.Rules;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.SortingRecipes.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.SortingRecipes.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Bulk update recipe priorities (e.g. after drag-and-drop reorder). Auto-save on drop.
    /// </summary>
    [HttpPatch("priorities")]
    public async Task<IActionResult> UpdatePriorities([FromBody] List<UpdateRecipePriorityDto> updates)
    {
        if (updates == null || updates.Count == 0)
            return BadRequest(new { error = "At least one recipe priority is required." });

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var ids = updates.Select(u => u.Id).ToList();
            var entities = await _dbContext.SortingRecipes.Where(r => ids.Contains(r.Id)).ToListAsync();

            foreach (var dto in updates)
            {
                var entity = entities.FirstOrDefault(e => e.Id == dto.Id);
                if (entity != null)
                {
                    entity.Priority = dto.Priority;
                    entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to update recipe priorities");
            throw;
        }
    }

    private static SortingRecipeResponseDto MapToResponse(SortingRecipe entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.Name,
        entity.Description,
        entity.FileTypePattern,
        entity.DestinationPathTemplate,
        entity.IsActive,
        entity.Priority,
        entity.CreatedBy,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.Rules,
        entity.FilesProcessedCount
    );
}
