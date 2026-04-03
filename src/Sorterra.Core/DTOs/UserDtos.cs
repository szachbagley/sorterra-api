namespace Sorterra.Core.DTOs;

public record CreateUserDto(
    string CognitoSub,
    string Email,
    string? DisplayName
);

public record UpdateUserDto(
    string? Email,
    string? DisplayName,
    DateTime? LastLoginAt
);

public record UserResponseDto(
    Guid Id,
    string CognitoSub,
    string Email,
    string? DisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt
);

public record UserProfileDto(
    UserResponseDto User,
    OrganizationResponseDto? Organization,
    string? Role
);
