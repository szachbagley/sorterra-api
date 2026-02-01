namespace Sorterra.Core.DTOs;

public record CreateUserOrganizationDto(
    Guid UserId,
    Guid OrganizationId,
    string? Role
);

public record UpdateUserOrganizationDto(
    string? Role
);

public record UserOrganizationResponseDto(
    Guid UserId,
    Guid OrganizationId,
    string Role,
    DateTime JoinedAt
);
