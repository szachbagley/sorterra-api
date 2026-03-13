using System.Text;
using System.Text.Json;
using Amazon.BedrockAgentCore;
using Amazon.BedrockAgentCore.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sorterra.Core.DTOs;
using Sorterra.Core.Entities;
using Sorterra.Infrastructure.Data;

namespace Sorterra.Api.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class SortController : ControllerBase
{
    private const string AgentRuntimeId = "arn:aws:bedrock-agentcore:us-east-1:896170900648:runtime/Sorterra-CqtsJb9h98";

    private readonly SorterraDbContext _dbContext;
    private readonly ILogger<SortController> _logger;
    private readonly IAmazonBedrockAgentCore _agentClient;

    public SortController(
        SorterraDbContext dbContext,
        ILogger<SortController> logger,
        IAmazonBedrockAgentCore agentClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _agentClient = agentClient;
    }

    [HttpPost]
    public async Task<IActionResult> TriggerSort(TriggerSortRequest request)
    {
        // --- 1. Look up the SharePoint connection ---
        var connection = await _dbContext.SharePointConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConnectionId);

        if (connection == null)
            return NotFound(new { error = "Connection not found" });

        if (string.IsNullOrEmpty(connection.TenantId))
            return BadRequest(new { error = "Connection is missing a TenantId" });

        // --- 2. Look up the recipe ---
        var recipe = await _dbContext.SortingRecipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId && r.IsActive);

        if (recipe == null)
            return NotFound(new { error = "Recipe not found or inactive" });

        // --- 3. Deserialize recipe rules ---
        string[] rules;
        try
        {
            rules = JsonSerializer.Deserialize<string[]>(recipe.Rules) ?? [];
        }
        catch (JsonException)
        {
            _logger.LogWarning("Recipe {RecipeId} has invalid rules JSON, sending as single rule", recipe.Id);
            rules = string.IsNullOrWhiteSpace(recipe.Rules) || recipe.Rules == "{}"
                ? []
                : [recipe.Rules];
        }

        if (rules.Length == 0)
            return BadRequest(new { error = "Recipe has no rules defined" });

        // --- 4. Build the agent payload ---
        var agentPayload = new
        {
            id = connection.OrganizationId.ToString(),
            tenant_id = connection.TenantId,
            site_url = connection.SiteUrl,
            path = request.FolderPath,
            recipe = new
            {
                name = recipe.Name,
                rules
            }
        };

        // --- 5. Invoke the agent via Bedrock AgentCore SDK ---
        _logger.LogInformation(
            "Triggering sort: Connection={ConnectionId}, Recipe={RecipeId}, Path={FolderPath}",
            request.ConnectionId, request.RecipeId, request.FolderPath);

        AgentResponse agentResponse;
        var sessionId = $"session-{Guid.NewGuid()}";

        try
        {
            agentResponse = await InvokeAgentAsync(agentPayload, sessionId);
        }
        catch (Amazon.BedrockAgentCore.Model.AccessDeniedException ex)
        {
            _logger.LogError(ex, "IAM permission denied when invoking the Sorterra agent");
            return StatusCode(502, new { error = "Permission denied when calling the sorting agent", detail = ex.Message });
        }
        catch (Amazon.BedrockAgentCore.Model.ResourceNotFoundException ex)
        {
            _logger.LogError(ex, "Agent runtime not found: {AgentId}", AgentRuntimeId);
            return StatusCode(502, new { error = "Sorting agent not found", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke the Sorterra agent");
            return StatusCode(502, new { error = "Failed to invoke the sorting agent", detail = ex.Message });
        }

        // --- 6. Check for agent-level error ---
        if (agentResponse.Status == "error")
        {
            _dbContext.ActivityLogs.Add(new ActivityLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = connection.OrganizationId,
                ActivityType = "sort_failed",
                EntityType = "SortingRecipe",
                EntityId = recipe.Id,
                Description = agentResponse.Message ?? "Agent returned an error",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return StatusCode(502, new { error = agentResponse.Message ?? "Sorting agent failed" });
        }

        // --- 7. Record results in the database ---
        var fileResults = new List<SortFileResultDto>();

        if (agentResponse.Results != null)
        {
            foreach (var r in agentResponse.Results)
            {
                var originalName = Path.GetFileName(r.File);
                var processedFile = new ProcessedFile
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = connection.OrganizationId,
                    ConnectionId = connection.Id,
                    SharePointItemId = r.File,  // server-relative URL as identifier
                    OriginalName = originalName,
                    OriginalPath = r.File,
                    NewPath = r.Status == "success" ? r.Result : null,
                    FileExtension = Path.GetExtension(originalName),
                    AppliedRecipeId = recipe.Id,
                    Status = r.Status,
                    ProcessedAt = DateTime.UtcNow,
                    ErrorMessage = r.Message,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.ProcessedFiles.Add(processedFile);

                fileResults.Add(new SortFileResultDto(
                    r.File,
                    r.Status,
                    r.Result,
                    r.Message,
                    processedFile.Id
                ));
            }
        }

        _dbContext.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = connection.OrganizationId,
            ActivityType = "sort_completed",
            EntityType = "SortingRecipe",
            EntityId = recipe.Id,
            Description = $"Sorted {agentResponse.FilesSorted}/{agentResponse.FilesFound} files in {request.FolderPath}",
            Metadata = JsonSerializer.Serialize(new
            {
                connectionId = connection.Id,
                recipeId = recipe.Id,
                folderPath = request.FolderPath,
                filesFound = agentResponse.FilesFound,
                filesSorted = agentResponse.FilesSorted
            }),
            CreatedAt = DateTime.UtcNow
        });

        // Increment the recipe's processed file count
        var trackedRecipe = await _dbContext.SortingRecipes.FindAsync(recipe.Id);
        if (trackedRecipe != null)
        {
            trackedRecipe.FilesProcessedCount += agentResponse.FilesSorted;
            trackedRecipe.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Sort complete: {FilesSorted}/{FilesFound} files sorted for connection {ConnectionId}",
            agentResponse.FilesSorted, agentResponse.FilesFound, request.ConnectionId);

        // --- 7. Return result to frontend ---
        return Ok(new SortResponseDto(
            agentResponse.Status,
            agentResponse.FilesFound,
            agentResponse.FilesSorted,
            request.ConnectionId,
            request.RecipeId,
            fileResults
        ));
    }

    /// <summary>
    /// Invoke the Sorterra agent via Bedrock AgentCore SDK.
    /// No credentials are needed — the ECS task role is picked up automatically.
    /// </summary>
    private async Task<AgentResponse> InvokeAgentAsync(object payload, string sessionId)
    {
        var request = new InvokeAgentRuntimeRequest
        {
            AgentRuntimeArn = AgentRuntimeId,
            RuntimeSessionId = sessionId,
            ContentType = "application/json",
            Accept = "application/json",
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))
        };

        var response = await _agentClient.InvokeAgentRuntimeAsync(request);

        using var reader = new StreamReader(response.Response);
        var json = await reader.ReadToEndAsync();

        return JsonSerializer.Deserialize<AgentResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Agent returned null response");
    }
}
