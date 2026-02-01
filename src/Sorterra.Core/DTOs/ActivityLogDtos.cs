namespace Sorterra.Core.DTOs;

public record CreateActivityLogDto(
    Guid OrganizationId,
    Guid? UserId,
    string ActivityType,
    string? EntityType,
    Guid? EntityId,
    string? Description,
    string? Metadata
);

public record UpdateActivityLogDto(
    string? Description,
    string? Metadata
);

public record ActivityLogResponseDto(
    Guid Id,
    Guid OrganizationId,
    Guid? UserId,
    string ActivityType,
    string? EntityType,
    Guid? EntityId,
    string? Description,
    string Metadata,
    DateTime CreatedAt
);
