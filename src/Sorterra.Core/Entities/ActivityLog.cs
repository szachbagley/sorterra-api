namespace Sorterra.Core.Entities;

public class ActivityLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }

    public string ActivityType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }

    public string? Description { get; set; }
    public string Metadata { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public User? User { get; set; }
}
