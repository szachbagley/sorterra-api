namespace Sorterra.Core.DTOs;

public record CreateProcessedFileDto(
    Guid OrganizationId,
    Guid? ConnectionId,
    string SharePointItemId,
    string? SharePointDriveId,
    string OriginalName,
    string? OriginalPath,
    string? FileExtension,
    long? FileSizeBytes,
    string? MimeType,
    string? ClassifiedType,
    decimal? ClassificationConfidence,
    Guid? AppliedRecipeId,
    string? Status,
    string? ExtractedMetadata
);

public record UpdateProcessedFileDto(
    string? NewName,
    string? NewPath,
    string? ClassifiedType,
    decimal? ClassificationConfidence,
    Guid? AppliedRecipeId,
    string? Status,
    DateTime? ProcessedAt,
    string? ErrorMessage,
    string? ExtractedMetadata
);

public record ProcessedFileResponseDto(
    Guid Id,
    Guid OrganizationId,
    Guid? ConnectionId,
    string SharePointItemId,
    string? SharePointDriveId,
    string OriginalName,
    string? NewName,
    string? OriginalPath,
    string? NewPath,
    string? FileExtension,
    long? FileSizeBytes,
    string? MimeType,
    string? ClassifiedType,
    decimal? ClassificationConfidence,
    Guid? AppliedRecipeId,
    string Status,
    DateTime? ProcessedAt,
    string? ErrorMessage,
    string ExtractedMetadata,
    DateTime CreatedAt
);
