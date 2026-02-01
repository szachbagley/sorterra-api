namespace Sorterra.Core.DTOs;

public record CreateOrganizationDto(
    string Name,
    string? Settings
);

public record UpdateOrganizationDto(
    string? Name,
    string? Settings
);

public record OrganizationResponseDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    string Settings
);
