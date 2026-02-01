namespace Sorterra.Core.DTOs;

public record CreateDocumentChunkDto(
    Guid ProcessedFileId,
    Guid OrganizationId,
    int ChunkIndex,
    string ChunkText,
    int? ChunkTokens,
    string? Embedding,
    int? PageNumber,
    string? SectionHeader
);

public record UpdateDocumentChunkDto(
    string? ChunkText,
    int? ChunkTokens,
    string? Embedding,
    int? PageNumber,
    string? SectionHeader
);

public record DocumentChunkResponseDto(
    Guid Id,
    Guid ProcessedFileId,
    Guid OrganizationId,
    int ChunkIndex,
    string ChunkText,
    int? ChunkTokens,
    string? Embedding,
    int? PageNumber,
    string? SectionHeader,
    DateTime CreatedAt
);
