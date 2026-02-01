namespace Sorterra.Core.DTOs;

public record CreateSearchQueryDto(
    Guid OrganizationId,
    Guid? UserId,
    string QueryText,
    string? QueryEmbedding,
    int? ResultsCount,
    int? LatencyMs,
    string? ClickedResultIds
);

public record UpdateSearchQueryDto(
    int? ResultsCount,
    int? LatencyMs,
    string? ClickedResultIds
);

public record SearchQueryResponseDto(
    Guid Id,
    Guid OrganizationId,
    Guid? UserId,
    string QueryText,
    string? QueryEmbedding,
    int? ResultsCount,
    int? LatencyMs,
    string? ClickedResultIds,
    DateTime CreatedAt
);
