using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Core.DTOs;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(SorterraDbContext dbContext, ILogger<OrganizationsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.Organizations.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.Organizations.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationDto dto)
    {
        var entity = new Organization
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Settings = dto.Settings ?? "{}",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Organizations.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateOrganizationDto dto)
    {
        var entity = await _dbContext.Organizations.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.Name is not null) entity.Name = dto.Name;
        if (dto.Settings is not null) entity.Settings = dto.Settings;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.Organizations.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.Organizations.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static OrganizationResponseDto MapToResponse(Organization entity) => new(
        entity.Id,
        entity.Name,
        entity.CreatedAt,
        entity.Settings
    );
}
