namespace Sorterra.Core.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid ProcessedFileId { get; set; }
    public Guid OrganizationId { get; set; }

    public int ChunkIndex { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int? ChunkTokens { get; set; }

    public string? Embedding { get; set; }

    public int? PageNumber { get; set; }
    public string? SectionHeader { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ProcessedFile ProcessedFile { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
