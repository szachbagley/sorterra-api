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
public class ProcessedFilesController : ControllerBase
{
    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<ProcessedFilesController> _logger;

    public ProcessedFilesController(SorterraDbContext dbContext, ILogger<ProcessedFilesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _dbContext.ProcessedFiles.ToListAsync();
        return Ok(entities.Select(MapToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _dbContext.ProcessedFiles.FindAsync(id);
        if (entity == null) return NotFound();
        return Ok(MapToResponse(entity));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProcessedFileDto dto)
    {
        var entity = new ProcessedFile
        {
            Id = Guid.NewGuid(),
            OrganizationId = dto.OrganizationId,
            ConnectionId = dto.ConnectionId,
            SharePointItemId = dto.SharePointItemId,
            SharePointDriveId = dto.SharePointDriveId,
            OriginalName = dto.OriginalName,
            OriginalPath = dto.OriginalPath,
            FileExtension = dto.FileExtension,
            FileSizeBytes = dto.FileSizeBytes,
            MimeType = dto.MimeType,
            ClassifiedType = dto.ClassifiedType,
            ClassificationConfidence = dto.ClassificationConfidence,
            AppliedRecipeId = dto.AppliedRecipeId,
            Status = dto.Status ?? "pending",
            ExtractedMetadata = dto.ExtractedMetadata ?? "{}",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProcessedFiles.Add(entity);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateProcessedFileDto dto)
    {
        var entity = await _dbContext.ProcessedFiles.FindAsync(id);
        if (entity == null) return NotFound();

        if (dto.NewName is not null) entity.NewName = dto.NewName;
        if (dto.NewPath is not null) entity.NewPath = dto.NewPath;
        if (dto.ClassifiedType is not null) entity.ClassifiedType = dto.ClassifiedType;
        if (dto.ClassificationConfidence is not null) entity.ClassificationConfidence = dto.ClassificationConfidence;
        if (dto.AppliedRecipeId is not null) entity.AppliedRecipeId = dto.AppliedRecipeId;
        if (dto.Status is not null) entity.Status = dto.Status;
        if (dto.ProcessedAt is not null) entity.ProcessedAt = dto.ProcessedAt;
        if (dto.ErrorMessage is not null) entity.ErrorMessage = dto.ErrorMessage;
        if (dto.ExtractedMetadata is not null) entity.ExtractedMetadata = dto.ExtractedMetadata;

        await _dbContext.SaveChangesAsync();
        return Ok(MapToResponse(entity));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _dbContext.ProcessedFiles.FindAsync(id);
        if (entity == null) return NotFound();

        _dbContext.ProcessedFiles.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static ProcessedFileResponseDto MapToResponse(ProcessedFile entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.ConnectionId,
        entity.SharePointItemId,
        entity.SharePointDriveId,
        entity.OriginalName,
        entity.NewName,
        entity.OriginalPath,
        entity.NewPath,
        entity.FileExtension,
        entity.FileSizeBytes,
        entity.MimeType,
        entity.ClassifiedType,
        entity.ClassificationConfidence,
        entity.AppliedRecipeId,
        entity.Status,
        entity.ProcessedAt,
        entity.ErrorMessage,
        entity.ExtractedMetadata,
        entity.CreatedAt
    );
}
