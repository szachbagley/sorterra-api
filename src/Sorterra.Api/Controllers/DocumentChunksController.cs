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
public class DocumentChunksController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<DocumentChunksController> _logger;

    public DocumentChunksController(SorterraDbContext dbContext, ILogger<DocumentChunksController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.DocumentChunks.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.DocumentChunks.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDocumentChunkDto dto)
    {
        var entity = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            ProcessedFileId = dto.ProcessedFileId,
            OrganizationId = dto.OrganizationId,
            ChunkIndex = dto.ChunkIndex,
            ChunkText = dto.ChunkText,
            ChunkTokens = dto.ChunkTokens,
            Embedding = dto.Embedding,
            PageNumber = dto.PageNumber,
            SectionHeader = dto.SectionHeader,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DocumentChunks.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateDocumentChunkDto dto)
    {
        var entity = await _dbContext.DocumentChunks.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.ChunkText is not null) entity.ChunkText = dto.ChunkText;
        if (dto.ChunkTokens is not null) entity.ChunkTokens = dto.ChunkTokens;
        if (dto.Embedding is not null) entity.Embedding = dto.Embedding;
        if (dto.PageNumber is not null) entity.PageNumber = dto.PageNumber;
        if (dto.SectionHeader is not null) entity.SectionHeader = dto.SectionHeader;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.DocumentChunks.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.DocumentChunks.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static DocumentChunkResponseDto MapToResponse(DocumentChunk entity) => new(
        entity.Id,
        entity.ProcessedFileId,
        entity.OrganizationId,
        entity.ChunkIndex,
        entity.ChunkText,
        entity.ChunkTokens,
        entity.Embedding,
        entity.PageNumber,
        entity.SectionHeader,
        entity.CreatedAt
    );
}
