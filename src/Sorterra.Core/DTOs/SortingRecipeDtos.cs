namespace Sorterra.Core.DTOs;

public record CreateSortingRecipeDto(
    Guid OrganizationId,
    string Name,
    string? Description,
    string? FileTypePattern,
    string? DestinationPathTemplate,
    bool? IsActive,
    int? Priority,
    Guid? CreatedBy,
    string? Rules
);

public record UpdateSortingRecipeDto(
    string? Name,
    string? Description,
    string? FileTypePattern,
    string? DestinationPathTemplate,
    bool? IsActive,
    int? Priority,
    string? Rules
);

public record SortingRecipeResponseDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    string? FileTypePattern,
    string? DestinationPathTemplate,
    bool IsActive,
    int Priority,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Rules,
    int FilesProcessedCount
);
