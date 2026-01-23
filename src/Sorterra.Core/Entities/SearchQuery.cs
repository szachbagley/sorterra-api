namespace Sorterra.Core.Entities;

public class SearchQuery
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }

    public string QueryText { get; set; } = string.Empty;
    public string? QueryEmbedding { get; set; }

    public int? ResultsCount { get; set; }
    public int? LatencyMs { get; set; }

    public string? ClickedResultIds { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public User? User { get; set; }
}
