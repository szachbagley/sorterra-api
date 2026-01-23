namespace Sorterra.Core.Entities;

public class OAuthToken
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public byte[] AccessTokenEncrypted { get; set; } = Array.Empty<byte>();
    public byte[] RefreshTokenEncrypted { get; set; } = Array.Empty<byte>();
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public string? Scope { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public SharePointConnection Connection { get; set; } = null!;
}
