# Agent ↔ API Integration Fixes

Discrepancies found between `sorterra-agent` (Python) and `sorterra-api` (C# .NET) as of 2026-03-09. Grouped by priority.

---

## Critical

### 1. Hardcoded Secrets Manager path breaks multi-tenant

**Agent file:** `core/sharepoint_connection/agent_tools.py:26`

`_get_sorter()` always fetches credentials from `sorterra/tenants/tenant_a`. Only `tenant_id` is overridden from the payload — `client_id`, `thumbprint`, `private_key`, and `site_url` are all locked to that one secret.

**Fix (agent):** Accept a tenant identifier in the payload and resolve it to a per-tenant secret path (e.g., `sorterra/tenants/{tenant_id}`), or accept credentials directly in the payload. At minimum, the `SHAREPOINT_SITE_URL` must vary per connection.

---

## High

### 2. `result` field contains LLM prose, stored as `NewPath`

**Agent file:** `core/agent.py:174`
**API file:** `Controllers/SortController.cs:131`

The agent sets `result` to `res['messages'][-1].content` — the LLM's natural-language summary (e.g., "I moved the file to Finance/Invoices/ and renamed it"). The API stores this in `ProcessedFile.NewPath`, which is meant to be an actual file path.

**Fix (agent):** Return a structured `destination_path` field with the actual server-relative URL the file was moved to, separate from the LLM description. The agent's tool calls (`move_document`, `secure_move_document`) already know the destination — capture it and return it.

**Fix (API):** Store the LLM description in a separate field (or in `ProcessedFile.ErrorMessage` repurposed as a general `details` field), and only put a real path in `NewPath`.

### 3. API returns 200 OK on agent-level errors

**API file:** `Controllers/SortController.cs:118–193`

When the agent returns `status: "error"` (e.g., folder not found, missing path), the API doesn't check it. It creates a misleading "Sorted 0/0 files" activity log and returns HTTP 200 with the error body.

**Fix (API):** After deserializing the agent response, check `agentResponse.Status`. If it's `"error"`, return an appropriate HTTP status (e.g., 502 or 422) and write a meaningful activity log entry (e.g., `sort_failed`).

### 4. Agent top-level `message` field silently dropped

**Agent file:** `core/agent.py:138,148,151`
**API file:** `Core/DTOs/SortDtos.cs:17–30`

The agent returns `{"status": "error", "message": "Failed to list folder: ..."}` but `AgentResponse` has no `message` property, so the error detail is lost.

**Fix (API):** Add a `message` property to `AgentResponse`:
```csharp
[JsonPropertyName("message")]
public string? Message { get; set; }
```
Use it in error responses and activity logs.

---

## Medium

### 5. `SiteUrl` from the connection is not sent to the agent

**API file:** `Controllers/SortController.cs:75–85`
**Agent file:** `core/sharepoint_connection/agent_tools.py:28`

Each `SharePointConnection` has a `SiteUrl`, but the agent reads `SHAREPOINT_SITE_URL` from Secrets Manager. If a connection points to a different site than the one in the secret, the agent operates on the wrong site.

**Fix:** Either send `site_url` in the payload and have the agent use it, or resolve it from the tenant secret. This is closely related to fix #1.

### 6. `id` semantic mismatch — `OrganizationId` sent as user ID

**API file:** `Controllers/SortController.cs:77`
**Agent file:** `core/agent.py:132`

The API sends `connection.OrganizationId` as `id`. The agent treats it as `user_id` for S3 log paths. All users in the same org share a log namespace.

**Fix:** Either rename to `organization_id` in the payload and agent, or send the actual Cognito user ID if per-user logging is desired.

### 7. Session ID reuse risks state contamination

**API file:** `Controllers/SortController.cs:93`

`session-{OrgId}` is deterministic. Concurrent sorts from the same org share a Bedrock AgentCore session, which could cause stale context or conflicts.

**Fix (API):** Use a unique session ID per request:
```csharp
var sessionId = $"session-{Guid.NewGuid()}";
```

---

## Low

### 8. `MAX_FILES=10` cap is silent

**Agent file:** `core/agent.py:128,154–156,189`

`files_found` reports the count after capping (not the original count). The API and frontend never know files were skipped.

**Fix (agent):** Return `files_found` as the original count and add a `files_capped` or `files_skipped` field:
```python
return {
    "status": "success",
    "files_found": original_count,
    "files_sorted": succeeded,
    "files_capped": len(file_urls),  # after cap
    "results": results
}
```

### 9. Inconsistent per-file result fields

**Agent file:** `core/agent.py:171–182`

Success results omit `message`; error results omit `result`. The nullable DTOs handle this, but returning both fields consistently would be cleaner.

**Fix (agent):** Always include both `result` and `message` in each result entry (set to `null` when not applicable).

### 10. Unused credential fields on `SharePointConnection` entity

**API file:** `Core/Entities/SharePointConnection.cs:9–12`

`ClientId`, `Thumbprint`, `PrivateKeyPath`, `DriveId` are stored in the database but never used during sort invocations. They may have been intended for a future per-connection credential flow.

**Fix:** Either implement per-connection credential forwarding (related to fix #1) or remove the unused fields to avoid confusion. Decide on the intended architecture first.

### 11. `SortingRecipe.Rules` defaults to `"{}"` (object), deserialized as `string[]`

**API file:** `Core/Entities/SortingRecipe.cs:16`
**API file:** `Controllers/SortController.cs:58–69`

The default `"{}"` triggers a `JsonException` on every new recipe until rules are set. The catch block handles it, but the default should match the expected format.

**Fix (API):** Change the default to `"[]"`:
```csharp
public string Rules { get; set; } = "[]";
```
