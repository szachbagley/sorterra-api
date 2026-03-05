using System.Text.Json.Serialization;

namespace Sorterra.Core.DTOs;

/// <summary>
/// Request body sent by the frontend to trigger a sort job.
/// </summary>
public record TriggerSortRequest(
    Guid ConnectionId,
    Guid RecipeId,
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
    Guid RecipeId,
    List<SortFileResultDto> Results
);

public record SortFileResultDto(
    string File,
    string Status,
    string? Result,
    string? Message,
    Guid? ProcessedFileId
);
