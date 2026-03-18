using System.Text.Json.Serialization;

namespace Sorterra.Core.DTOs;

/// <summary>
/// Request body sent by the frontend to trigger a sort job.
/// All active recipes for the connection's organization are merged and sent to the agent.
/// </summary>
public record TriggerSortRequest(
    Guid ConnectionId,
    string FolderPath
);

/// <summary>
/// Top-level response returned by the Sorterra agent.
/// </summary>
public class AgentResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("files_found")]
    public int FilesFound { get; set; }

    [JsonPropertyName("files_sorted")]
    public int FilesSorted { get; set; }

    [JsonPropertyName("results")]
    public List<AgentFileResult> Results { get; set; } = new();

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("stats")]
    public AgentStats? Stats { get; set; }
}

public class AgentStats
{
    [JsonPropertyName("total_tokens_consumed")]
    public int TotalTokensConsumed { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
}

/// <summary>
/// Per-file result from the agent.
/// </summary>
public class AgentFileResult
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Response returned by the SortController to the frontend.
/// Wraps the agent response with sort job metadata.
/// </summary>
public record SortResponseDto(
    string Status,
    int FilesFound,
    int FilesSorted,
    Guid ConnectionId,
    List<SortFileResultDto> Results,
    AgentStats? Stats = null
);

public record SortFileResultDto(
    string File,
    string Status,
    string? Result,
    string? Message,
    Guid? ProcessedFileId
);
