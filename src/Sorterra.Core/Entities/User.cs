namespace Sorterra.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public string CognitoSub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
}
