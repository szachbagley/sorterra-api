namespace Sorterra.Core.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Settings { get; set; } = "{}";

    // Navigation properties
    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
    public ICollection<SharePointConnection> SharePointConnections { get; set; } = new List<SharePointConnection>();
    public ICollection<SortingRecipe> SortingRecipes { get; set; } = new List<SortingRecipe>();
    public ICollection<ProcessedFile> ProcessedFiles { get; set; } = new List<ProcessedFile>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}
