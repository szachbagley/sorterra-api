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

        // --- 2. Load all active recipes for this connection's organization (merged into one sort) ---
        var activeRecipes = await _dbContext.SortingRecipes
            .AsNoTracking()
            .Where(r => r.OrganizationId == connection.OrganizationId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        if (activeRecipes.Count == 0)
            return BadRequest(new { error = "No active recipes found for this connection. Add and enable at least one recipe." });

        // --- 3. Merge rules from all active recipes (one name + one list for the agent) ---
        var mergedRules = new List<string>();
        foreach (var recipe in activeRecipes)
        {
            string[] recipeRules;
            try
            {
                recipeRules = JsonSerializer.Deserialize<string[]>(recipe.Rules) ?? [];
            }
            catch (JsonException)
            {
                if (!string.IsNullOrWhiteSpace(recipe.Rules) && recipe.Rules != "{}")
                    recipeRules = [recipe.Rules];
                else
                    recipeRules = [];
            }

            if (recipeRules.Length > 0)
            {
                mergedRules.Add($"[Recipe: {recipe.Name}]");
                mergedRules.AddRange(recipeRules);
            }
        }

        if (mergedRules.Count == 0)
            return BadRequest(new { error = "Active recipes have no rules defined. Add instructions to at least one recipe." });

        // --- 4. Build the agent payload (single name + merged rules) ---
        var agentPayload = new
        {
            id = connection.OrganizationId.ToString(),
            tenant_id = connection.TenantId,
            site_url = connection.SiteUrl,
            path = request.FolderPath,
            recipe = new
            {
                name = "Sorterra",
                rules = mergedRules
            }
        };

        // --- 5. Invoke the agent via Bedrock AgentCore SDK ---
        _logger.LogInformation(
            "Triggering sort: Connection={ConnectionId}, Path={FolderPath}, ActiveRecipes={Count}",
            request.ConnectionId, request.FolderPath, activeRecipes.Count);

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
                EntityId = Guid.Empty,
                Description = agentResponse.Message ?? "Agent returned an error",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            return StatusCode(502, new { error = agentResponse.Message ?? "Sorting agent failed" });
        }

        // --- 7. Record results in the database (combined sort: no single AppliedRecipeId) ---
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
                    SharePointItemId = r.File,
                    OriginalName = originalName,
                    OriginalPath = r.File,
                    NewPath = r.Status == "success" ? r.Result : null,
                    FileExtension = Path.GetExtension(originalName),
                    AppliedRecipeId = null, // combined sort uses all active recipes
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
            EntityId = Guid.Empty,
            Description = $"Sorted {agentResponse.FilesSorted}/{agentResponse.FilesFound} files in {request.FolderPath}",
            Metadata = JsonSerializer.Serialize(new
            {
                connectionId = connection.Id,
                folderPath = request.FolderPath,
                filesFound = agentResponse.FilesFound,
                filesSorted = agentResponse.FilesSorted,
                recipeCount = activeRecipes.Count
            }),
            CreatedAt = DateTime.UtcNow
        });

        // Increment processed file count for all active recipes that participated
        foreach (var recipe in activeRecipes)
        {
            var tracked = await _dbContext.SortingRecipes.FindAsync(recipe.Id);
            if (tracked != null)
            {
                tracked.FilesProcessedCount += agentResponse.FilesSorted;
                tracked.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Sort complete: {FilesSorted}/{FilesFound} files sorted for connection {ConnectionId}",
            agentResponse.FilesSorted, agentResponse.FilesFound, request.ConnectionId);

        // --- 8. Return result to frontend ---
        return Ok(new SortResponseDto(
            agentResponse.Status,
            agentResponse.FilesFound,
            agentResponse.FilesSorted,
            request.ConnectionId,
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
