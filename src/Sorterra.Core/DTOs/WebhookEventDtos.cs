namespace Sorterra.Core.DTOs;

public record CreateWebhookEventDto(
    Guid ConnectionId,
    string? EventType,
    string? ResourceType,
    string? ResourceId,
    string RawPayload,
    string? ProcessingStatus
);

public record UpdateWebhookEventDto(
    string? ProcessingStatus,
    DateTime? ProcessedAt,
    string? ErrorMessage
);

public record WebhookEventResponseDto(
    Guid Id,
    Guid ConnectionId,
    string? EventType,
    string? ResourceType,
    string? ResourceId,
    string RawPayload,
    string ProcessingStatus,
    DateTime? ProcessedAt,
    string? ErrorMessage,
    DateTime ReceivedAt
);
