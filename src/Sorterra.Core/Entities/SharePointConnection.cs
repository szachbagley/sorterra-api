namespace Sorterra.Core.Entities;

public class SharePointConnection
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string SiteUrl { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? Thumbprint { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? DriveId { get; set; }
    public string? SourceFolder { get; set; }
    public string ConnectionStatus { get; set; } = "pending";
    public DateTime? LastSyncAt { get; set; }
    public string? WebhookSubscriptionId { get; set; }
    public DateTime? WebhookExpiration { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public User? Creator { get; set; }
    public OAuthToken? OAuthToken { get; set; }
}
