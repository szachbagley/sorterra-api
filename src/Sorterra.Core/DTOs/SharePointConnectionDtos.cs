namespace Sorterra.Core.DTOs;

public record CreateSharePointConnectionDto(
    Guid? OrganizationId,
    string SiteUrl,
    string? TenantId,
    string? ClientId,
    string? Thumbprint,
    string? PrivateKeyPath,
    string? DriveId,
    string? SourceFolder,
    string? ConnectionStatus,
    Guid? CreatedBy
);

public record UpdateSharePointConnectionDto(
    string? SiteUrl,
    string? TenantId,
    string? ClientId,
    string? Thumbprint,
    string? PrivateKeyPath,
    string? DriveId,
    string? SourceFolder,
    string? ConnectionStatus,
    DateTime? LastSyncAt,
    string? WebhookSubscriptionId,
    DateTime? WebhookExpiration,
    string? ErrorMessage
);

public record SharePointConnectionResponseDto(
    Guid Id,
    Guid OrganizationId,
    string SiteUrl,
    string? TenantId,
    string? ClientId,
    string? Thumbprint,
    string? PrivateKeyPath,
    string? DriveId,
    string? SourceFolder,
    string ConnectionStatus,
    DateTime? LastSyncAt,
    string? WebhookSubscriptionId,
    DateTime? WebhookExpiration,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? ErrorMessage
);
