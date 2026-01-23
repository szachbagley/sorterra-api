namespace Sorterra.Core.Entities;

public class WebhookEvent
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }

    public string? EventType { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }

    public string RawPayload { get; set; } = "{}";

    public string ProcessingStatus { get; set; } = "received";
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime ReceivedAt { get; set; }

    // Navigation properties
    public SharePointConnection Connection { get; set; } = null!;
}
