namespace Sorterra.Core.Entities;

public class UserOrganization
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Role { get; set; } = "member";
    public DateTime JoinedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
