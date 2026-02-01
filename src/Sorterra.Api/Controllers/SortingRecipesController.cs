using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Core.DTOs;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.SortingRecipes.ToListAsync();
        return Ok(entities.Select(MapToResponse));
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
            Rules = dto.Rules ?? "{}",
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
