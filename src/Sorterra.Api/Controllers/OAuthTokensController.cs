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
public class OAuthTokensController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<OAuthTokensController> _logger;

    public OAuthTokensController(SorterraDbContext dbContext, ILogger<OAuthTokensController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.OAuthTokens.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.OAuthTokens.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOAuthTokenDto dto)
    {
        var entity = new OAuthToken
        {
            Id = Guid.NewGuid(),
            ConnectionId = dto.ConnectionId,
            AccessTokenEncrypted = dto.AccessTokenEncrypted,
            RefreshTokenEncrypted = dto.RefreshTokenEncrypted,
            TokenType = dto.TokenType ?? "Bearer",
            ExpiresAt = dto.ExpiresAt,
            Scope = dto.Scope,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.OAuthTokens.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateOAuthTokenDto dto)
    {
        var entity = await _dbContext.OAuthTokens.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.AccessTokenEncrypted is not null) entity.AccessTokenEncrypted = dto.AccessTokenEncrypted;
        if (dto.RefreshTokenEncrypted is not null) entity.RefreshTokenEncrypted = dto.RefreshTokenEncrypted;
        if (dto.TokenType is not null) entity.TokenType = dto.TokenType;
        if (dto.ExpiresAt is not null) entity.ExpiresAt = dto.ExpiresAt.Value;
        if (dto.Scope is not null) entity.Scope = dto.Scope;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.OAuthTokens.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.OAuthTokens.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static OAuthTokenResponseDto MapToResponse(OAuthToken entity) => new(
        entity.Id,
        entity.ConnectionId,
        entity.TokenType,
        entity.ExpiresAt,
        entity.Scope,
        entity.CreatedAt,
        entity.UpdatedAt
    );
}
