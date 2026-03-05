# Sort Endpoint — Agent Integration

Step-by-step guide for building the `POST /api/sort` endpoint that lets the frontend trigger the Sorterra agent to sort files in a SharePoint folder. The API acts as the orchestrator: it looks up the connection and recipe from the database, invokes the agent via the AWS Bedrock AgentCore SDK, records the results, and returns them to the frontend.

## Prerequisites

- The agent is deployed to Bedrock AgentCore (agent runtime ID: `Sorterra-CqtsJb9h98`)
- The API's ECS task role has permission to call `bedrock-agentcore:InvokeAgentRuntime` (confirm with McKay/Nathan if not already in the policy)

---

## Phase 1: Backend — DTOs and Agent Response Models

Add the request/response types the SortController will use.

### Step 1: Create `SortDtos.cs`

Create a new file at `src/Sorterra.Core/DTOs/SortDtos.cs`:

```csharp
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
```

Notes:
- `TriggerSortRequest` is what the frontend sends.
- `AgentResponse` / `AgentFileResult` deserialize the agent's JSON response.
- `SortResponseDto` / `SortFileResultDto` are what the API returns to the frontend. `ProcessedFileId` links each result to the `ProcessedFile` record created in the database, so the frontend can navigate to the file detail.

---

## Phase 2: Backend — NuGet Package and Service Registration

Install the AWS SDK package and register the Bedrock AgentCore client. No endpoint URLs or API keys are needed — the SDK picks up credentials automatically from the ECS task role.

### Step 1: Add the `AWSSDK.BedrockAgentCore` NuGet package

From the repo root:

```bash
dotnet add src/Sorterra.Api/Sorterra.Api.csproj package AWSSDK.BedrockAgentCore
```

### Step 2: Register the SDK client in `Program.cs`

In `src/Sorterra.Api/Program.cs`, add this after the existing service registrations (e.g., after `AddControllers()`):

```csharp
// Bedrock AgentCore client for invoking the Sorterra agent
builder.Services.AddSingleton<Amazon.BedrockAgentCore.IAmazonBedrockAgentCore>(
    _ => new Amazon.BedrockAgentCore.AmazonBedrockAgentCoreClient(
        Amazon.RegionEndpoint.USEast1));
```

The client is registered as a singleton — it's thread-safe and reuses TCP connections. No credentials are passed; the SDK resolves them from the ECS task role automatically.

---

## Phase 3: Backend — SortController

Build the controller that ties everything together.

### Step 1: Create `SortController.cs`

Create a new file at `src/Sorterra.Api/Controllers/SortController.cs`:

```csharp
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
    // Full ARN is required — the short ID alone causes:
    // "accountID is required when agentRuntimeArn is provided as agentId instead of agentRuntimeArn"
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
        var sessionId = $"session-{connection.OrganizationId}";

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

        // --- 6. Record results in the database ---
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
```

### Step 2: Verify it compiles

From the repo root:

```bash
dotnet build src/Sorterra.Api/Sorterra.Api.csproj
```

Fix any compilation errors before proceeding.

---

## Phase 4: Backend — IAM Policy for Agent Invocation

The API invokes the agent through the AWS SDK, which authenticates via the ECS task role. No endpoint URLs or API keys are needed — but the task role must have permission to call `bedrock-agentcore:InvokeAgentRuntime`.

### Step 1: Attach the IAM policy to the ECS task role

The API's ECS task role needs the following policy. This grants permission to invoke the specific Sorterra agent runtime and nothing else.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "bedrock-agentcore:InvokeAgentRuntime"
      ],
      "Resource": "arn:aws:bedrock-agentcore:us-east-1:896170900648:runtime/Sorterra-CqtsJb9h98*"
    }
  ]
}
```

> **Important:** The trailing `*` wildcard is required. The SDK calls a sub-resource at `.../runtime/Sorterra-CqtsJb9h98/runtime-endpoint/DEFAULT`, so an exact match on the runtime ARN alone will result in an `AccessDeniedException`.

To attach it:

1. Find the API's ECS **task role** (not the execution role). The task role is the one referenced by `taskRoleArn` in the task definition — likely named something like `sorterra-ecs-task-role`.
2. Create an inline policy or managed policy with the JSON above.
3. Attach it to the task role.

Via the CLI:

```bash
# Create the policy
aws iam create-policy \
  --policy-name sorterra-invoke-agent \
  --policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Action": ["bedrock-agentcore:InvokeAgentRuntime"],
        "Resource": "arn:aws:bedrock-agentcore:us-east-1:896170900648:runtime/Sorterra-CqtsJb9h98*"
      }
    ]
  }' \
  --region us-east-1

# Attach it to the task role
aws iam attach-role-policy \
  --role-name sorterra-ecs-task-role \
  --policy-arn arn:aws:iam::896170900648:policy/sorterra-invoke-agent
```

> **Note:** If you're unsure of the task role name, find it from the task definition:
> ```bash
> aws ecs describe-task-definition \
>   --task-definition sorterra-api \
>   --query "taskDefinition.taskRoleArn" \
>   --output text --region us-east-1
> ```

### Step 2: Verify outbound internet access

The SDK calls the Bedrock AgentCore API endpoint over HTTPS (the AWS public API, not a direct connection to the agent container). The API's ECS task needs outbound internet access to reach `bedrock-agentcore.us-east-1.amazonaws.com`. This is likely already the case if the API can reach Cognito for JWT validation — but confirm the subnet has a NAT gateway or the task uses a public subnet.

---

## Phase 5: Deploy and Verify the Backend

### Step 1: Build, push, and deploy

Follow the standard deployment process from [aws-ecs-update-redeployment.md](aws-ecs-update-redeployment.md):

```bash
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export AWS_REGION=us-east-1
export ECR_BASE=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

# Authenticate
aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin $ECR_BASE

# Build and push
docker build --platform linux/amd64 -t sorterra-api -f docker/api/Dockerfile .
docker tag sorterra-api:latest $ECR_BASE/sorterra-api:latest
docker push $ECR_BASE/sorterra-api:latest

# Deploy
aws ecs update-service \
  --cluster sorterra \
  --service sorterra-api-v2 \
  --force-new-deployment \
  --region $AWS_REGION

aws ecs wait services-stable \
  --cluster sorterra \
  --services sorterra-api-v2 \
  --region $AWS_REGION
```

### Step 2: Verify the endpoint exists

```bash
# Should return 401 (auth required) — confirms the route is registered, not 404
curl -s -o /dev/null -w "%{http_code}" -X POST https://sorterra.app/api/sort
```

A `401` means the route exists and auth is working. A `404` means the controller isn't registered — check the deploy logs.

### Step 3: Test with a real request (requires a valid JWT)

```bash
TOKEN="<your-cognito-jwt>"

curl -s -X POST https://sorterra.app/api/sort \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "connectionId": "<guid-of-a-sharepoint-connection>",
    "recipeId": "<guid-of-an-active-recipe>",
    "folderPath": "/sites/Sorterra/Shared Documents/AWS_test/"
  }' | python3 -m json.tool
```

Expected success response:

```json
{
  "status": "success",
  "filesFound": 5,
  "filesSorted": 4,
  "connectionId": "...",
  "recipeId": "...",
  "results": [
    {
      "file": "/sites/.../invoice.pdf",
      "status": "success",
      "result": "Moved to Finance/Invoices/AWS",
      "message": null,
      "processedFileId": "..."
    }
  ]
}
```

If the IAM policy from Phase 4 isn't attached yet, you'll get a `502` with `"Permission denied when calling the sorting agent"`. If the agent runtime ID is wrong, you'll get `"Sorting agent not found"`.

> **Tested 2026-03-05:** The endpoint returned `200` with `status: "error"` and `filesFound: 0` using seed/test data (no real SharePoint site). This confirms the full pipeline works: JWT auth, DB lookups, recipe rules parsing, agent invocation via SDK, response deserialization, and client response. Two fixes were required during testing:
> 1. The SDK requires the full ARN for `AgentRuntimeArn`, not just the short ID.
> 2. The IAM policy resource needs a trailing `*` wildcard to cover the `/runtime-endpoint/DEFAULT` sub-resource.

---

## Phase 6: Frontend — Sort API Service

Add the client-side API call.

### Step 1: Create `src/api/sort.js`

```js
import apiClient from './client';

export const sortApi = {
  async triggerSort(connectionId, recipeId, folderPath) {
    return await apiClient.post('/api/sort', {
      connectionId,
      recipeId,
      folderPath,
    });
  },
};
```

---

## Phase 7: Frontend — Sort Now UI

Add a "Sort Now" button to the Settings page that lets users trigger a sort on a SharePoint connection.

### Step 1: Add a "Sort Now" button to the connections list

In `src/pages/Settings.jsx`, add a "Sort Now" button next to each SharePoint connection in the connections table/list. When clicked, it opens a modal.

### Step 2: Build the SortModal component

Create `src/components/SortModal.jsx` with these fields:

- **Connection** — pre-selected from whichever row the user clicked (display the site URL, read-only)
- **Recipe** — dropdown populated by `recipesApi.getAll({ organizationId, isActive: true })`. Show recipe name and description.
- **Folder path** — text input, pre-populated with the connection's `sourceFolder` if available. The user can edit it. Placeholder: `/sites/SiteName/Shared Documents/FolderName/`
- **Sort button** — calls `sortApi.triggerSort(connectionId, recipeId, folderPath)`

### Step 3: Handle loading and results

While the sort is running:
- Disable the Sort button and show a spinner/loading state
- Display a message like "Sorting files... this may take a minute"

When the response comes back:
- Show a summary: "Sorted 4/5 files"
- List each file result with a success/error indicator
- On success, show a toast notification
- On error (502), show an error toast with the `error` field from the response body (e.g., "Permission denied when calling the sorting agent", "Failed to invoke the sorting agent")

### Step 4: Refresh data after sort

After a successful sort, refresh the data on the page so the user can see the results:
- If on the Settings page: the connection's status or last-sync time may have changed
- The Files page (`/files`) will now have new `ProcessedFile` records — consider navigating the user there or showing a link

---

## Phase 8: Verification Checklist

Run through this checklist to confirm the full integration is working.

- [ ] `POST /api/sort` returns `401` without a JWT
- [ ] `POST /api/sort` returns `404` for a non-existent `connectionId`
- [ ] `POST /api/sort` returns `404` for an inactive `recipeId`
- [ ] `POST /api/sort` returns `400` if the recipe has no rules
- [ ] `POST /api/sort` returns `400` if the connection has no `tenantId`
- [ ] `POST /api/sort` returns `502` if the IAM policy is missing or the agent runtime is not found
- [ ] `POST /api/sort` returns `200` with file results on a successful sort
- [ ] `ProcessedFile` records appear in `GET /api/processedfiles` after a sort
- [ ] An `ActivityLog` entry with `activityType: "sort_completed"` appears in `GET /api/activitylogs`
- [ ] The recipe's `filesProcessedCount` increments after a sort
- [ ] The frontend "Sort Now" button opens the SortModal
- [ ] The SortModal loads active recipes in the dropdown
- [ ] The SortModal shows loading state during the sort
- [ ] The SortModal displays results after the sort completes
- [ ] The Files page shows the newly processed files

---

## Coordination with Nathan

Most of the agent integration details have been confirmed. Remaining items:

1. **Confirm the agent runtime ID** — the code uses `Sorterra-CqtsJb9h98` (from Nathan's message). Verify this is the production runtime ID and won't change.
2. **Confirm the ECS task role name** — needed to attach the IAM policy in Phase 4. Find it from the task definition if unsure.
3. **Timeout expectations** — how long does a typical folder sort take? If large folders take more than a few minutes, consider adding a frontend polling mechanism or moving to async processing in a future phase.
