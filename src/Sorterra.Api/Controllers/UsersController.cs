using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Core.DTOs;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<UsersController> _logger;

    public UsersController(SorterraDbContext dbContext, ILogger<UsersController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.Users.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.Users.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto)
    {
        var entity = new User
        {
            Id = Guid.NewGuid(),
            CognitoSub = dto.CognitoSub,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserDto dto)
    {
        var entity = await _dbContext.Users.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.Email is not null) entity.Email = dto.Email;
        if (dto.DisplayName is not null) entity.DisplayName = dto.DisplayName;
        if (dto.LastLoginAt is not null) entity.LastLoginAt = dto.LastLoginAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.Users.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.Users.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static UserResponseDto MapToResponse(User entity) => new(
        entity.Id,
        entity.CognitoSub,
        entity.Email,
        entity.DisplayName,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.LastLoginAt
    );
}
