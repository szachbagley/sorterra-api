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
public class UserOrganizationsController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<UserOrganizationsController> _logger;

    public UserOrganizationsController(SorterraDbContext dbContext, ILogger<UserOrganizationsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.UserOrganizations.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{userId}/{organizationId}")]
    public async Task<IActionResult> GetById(Guid userId, Guid organizationId)
    {
        var entity = await _dbContext.UserOrganizations.FindAsync(userId, organizationId);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserOrganizationDto dto)
    {
        var entity = new UserOrganization
        {
            UserId = dto.UserId,
            OrganizationId = dto.OrganizationId,
            Role = dto.Role ?? "member",
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.UserOrganizations.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById),
            new { userId = entity.UserId, organizationId = entity.OrganizationId },
            MapToResponse(entity));
    }

    [HttpPut("{userId}/{organizationId}")]
    public async Task<IActionResult> Update(Guid userId, Guid organizationId, UpdateUserOrganizationDto dto)
    {
        var entity = await _dbContext.UserOrganizations.FindAsync(userId, organizationId);
        if (entity == null) return NotFound();

        if (dto.Role is not null) entity.Role = dto.Role;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{userId}/{organizationId}")]
    public async Task<IActionResult> Delete(Guid userId, Guid organizationId)
    {
        var entity = await _dbContext.UserOrganizations.FindAsync(userId, organizationId);
        if (entity == null) return NotFound();

        _dbContext.UserOrganizations.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static UserOrganizationResponseDto MapToResponse(UserOrganization entity) => new(
        entity.UserId,
        entity.OrganizationId,
        entity.Role,
        entity.JoinedAt
    );
}
