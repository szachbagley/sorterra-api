namespace Sorterra.Core.Entities;

public class ProcessedFile
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ConnectionId { get; set; }

    public string SharePointItemId { get; set; } = string.Empty;
    public string? SharePointDriveId { get; set; }

    public string OriginalName { get; set; } = string.Empty;
    public string? NewName { get; set; }
    public string? OriginalPath { get; set; }
    public string? NewPath { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? MimeType { get; set; }

    public string? ClassifiedType { get; set; }
    public decimal? ClassificationConfidence { get; set; }
    public Guid? AppliedRecipeId { get; set; }

    public string Status { get; set; } = "pending";
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string ExtractedMetadata { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public SharePointConnection? Connection { get; set; }
    public SortingRecipe? AppliedRecipe { get; set; }
    public ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();
}
