namespace Sorterra.Core.DTOs;

public record CreateOAuthTokenDto(
    Guid ConnectionId,
    byte[] AccessTokenEncrypted,
    byte[] RefreshTokenEncrypted,
    string? TokenType,
    DateTime ExpiresAt,
    string? Scope
);

public record UpdateOAuthTokenDto(
    byte[]? AccessTokenEncrypted,
    byte[]? RefreshTokenEncrypted,
    string? TokenType,
    DateTime? ExpiresAt,
    string? Scope
);

public record OAuthTokenResponseDto(
    Guid Id,
    Guid ConnectionId,
    string TokenType,
    DateTime ExpiresAt,
    string? Scope,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
