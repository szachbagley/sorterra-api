namespace Sorterra.Core.Entities;

public class SortingRecipe
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileTypePattern { get; set; }
    public string? DestinationPathTemplate { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Rules { get; set; } = "{}";
    public int FilesProcessedCount { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public User? Creator { get; set; }
}
