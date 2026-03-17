# Sorterra Integration Plan

Consolidated plan covering admin consent onboarding (from `app.py` prototype) and all agent ↔ API discrepancy fixes. Created 2026-03-09. Updated 2026-03-17 with implementation status.

---

## Architecture Context

The sort flow today:

```
Frontend → API (SortController) → Bedrock AgentCore → Agent (Python)
                                                        ↓
                                                  AWS Secrets Manager
                                                  (sorterra/tenants/tenant_a)
                                                        ↓
                                                  SharePoint REST API
                                                  (certificate auth via MSAL)
```

The agent authenticates to SharePoint using a **single hardcoded secret** containing one app registration's `CLIENT_ID`, `THUMBPRINT`, `PRIVATE_KEY`, `TENANT_ID`, and `SHAREPOINT_SITE_URL`. The `tenant_id` is overridden from the API payload, but all other credentials are locked to that one secret.

The `app.py` prototype demonstrates the **Azure AD Admin Consent** flow for multi-tenant onboarding — a browser redirect to `login.microsoftonline.com/common/adminconsent` that captures the customer's `tenant_id` after an admin grants consent.

The goal of this plan is to:
1. Integrate the admin consent flow into the API and frontend
2. Fix all known discrepancies between the agent and API
3. Move toward a multi-tenant architecture where one app registration serves all tenants

---

## Prerequisites

Confirm with Nathan:

- [ ] The Sorterra Azure AD app registration is configured as **multi-tenant** (supported account types: "Accounts in any organizational directory")
- [ ] The app has **SharePoint** application permissions: `Sites.FullControl.All`
- [ ] The app's certificate (thumbprint + private key) is stored in the global Secrets Manager secret and will work for any consented tenant
- [ ] The redirect URI `https://sorterra.app/api/auth/sharepoint/callback` is registered in the Azure AD app's redirect URIs
- [x] Client ID obtained: `120d6fc7-f18a-4507-ad34-6ae1d41bd0db`

If the app registration is currently single-tenant, Nathan will need to change it to multi-tenant in Azure AD before the consent flow will work for external tenants.

---

## Phase 1 — Admin Consent Flow (API + Frontend) ✅ IMPLEMENTED

Integrated the `app.py` prototype into the C# API and React frontend so users can onboard their SharePoint tenant through the browser. Implemented 2026-03-17.

### 1.1 API: Add global app configuration ✅

Added to `appsettings.json`:

```json
{
  "AzureAd": {
    "ClientId": "120d6fc7-f18a-4507-ad34-6ae1d41bd0db",
    "ConsentRedirectUri": "https://sorterra.app/api/auth/sharepoint/callback"
  },
  "FrontendBaseUrl": "http://localhost:3000"
}
```

Note: The redirect URI includes `/api` due to `app.UsePathBase("/api")`.

### 1.2 API: Create consent endpoints ✅

Created `SharePointAuthController` with two endpoints:

**`GET /api/auth/sharepoint/consent`** — Initiates the admin consent flow.
- Generates a `state` nonce (UUID), stores it server-side (cache or short-lived DB row) associated with the authenticated user's org
- Returns a JSON response with the consent URL:
  ```json
  {
    "consentUrl": "https://login.microsoftonline.com/common/adminconsent?client_id=...&state=...&redirect_uri=..."
  }
  ```
- The frontend opens this URL (redirect or popup)

**`GET /api/auth/sharepoint/callback`** — Handles the Microsoft redirect.
- Validates the `state` parameter against the stored nonce
- Checks for `error` / `error_description` query params
- Extracts `tenant` (tenant ID) and `admin_consent` from query params
- On success: creates a new `SharePointConnection` with `TenantId` set and `ConnectionStatus = "consented"`, or updates an existing pending connection
- Redirects the browser back to `https://sorterra.app/settings?consent=success` (or `?consent=error&message=...`)

**State storage:** Implemented as a signed JWT (`ConsentStateService.cs`) containing `{ connectionId, nonce, exp }`, signed with HMAC-SHA256 using `Encryption:TokenEncryptionKey`. Stateless — no DB or cache needed. 10-minute expiry.

### 1.3 Frontend: Replace manual Tenant ID input with consent button ✅

Reworked `ConnectionModal.jsx`:

1. Remove the `tenantId` text input field
2. Add a **"Connect with Microsoft"** button that:
   - Calls `GET /api/auth/sharepoint/consent` to get the consent URL
   - Opens the consent URL in a new window or redirects the current tab
3. The user enters `siteUrl` and `sourceFolder` before or after consenting
4. The note on line 118–121 ("Full OAuth authentication will be configured in a future update") can be removed

**Alternative flow** — two-step modal:
1. User enters `siteUrl` and `sourceFolder`, clicks "Next"
2. Modal shows "Authorize with Microsoft" button, user clicks it
3. After consent callback, connection is created with `tenantId` auto-populated

### 1.4 Frontend: Handle callback query params ✅

Instead of a separate callback route, `Settings.jsx` reads `consent=success` or `consent=error` from query params on mount (the API callback redirects to `/settings?consent=...`). Shows a toast and cleans up the URL params.

### 1.5 Connection status lifecycle ✅

Formalized connection status values (implemented in frontend `STATUS_CONFIG` and backend):

| Status | Meaning |
|--------|---------|
| `pending` | Connection created, no consent yet |
| `consented` | Admin consent granted, tenant ID captured |
| `active` | Fully operational — agent can sort files for this connection |
| `error` | Something is wrong (see `ErrorMessage` field) |

The `Sort Now` button is disabled for `pending` and `error` connections.

---

## Phase 2 — Agent Payload & Multi-Tenant Fixes (API-side done, agent-side pending)

Fix the critical and high-severity discrepancies between the API and agent. API-side changes implemented 2026-03-17. Agent-side changes require coordination with Nathan (we cannot modify sorterra-agent).

### 2.1 Send `site_url` in the agent payload — API ✅ / Agent pending

**Files:** `SortController.cs:75–85`, `agent.py:124–135`, `agent_tools.py:21–54`

The API has `connection.SiteUrl` but doesn't send it. The agent reads `SHAREPOINT_SITE_URL` from the hardcoded secret, which breaks if connections point to different sites.

**API change** — add `site_url` to the payload:
```csharp
var agentPayload = new
{
    id = connection.OrganizationId.ToString(),
    tenant_id = connection.TenantId,
    site_url = connection.SiteUrl,          // NEW
    path = request.FolderPath,
    recipe = new { name = recipe.Name, rules }
};
```

**Agent change** — use `site_url` from payload instead of secret:
```python
site_url = payload.get('site_url') or sp_secrets.get("SHAREPOINT_SITE_URL")
```

This resolves fix #1 (hardcoded secret) and fix #5 (SiteUrl not sent) from the fixes doc. The agent still reads `CLIENT_ID`, `THUMBPRINT`, and `PRIVATE_KEY` from the global secret (which is correct for a multi-tenant single-app-registration architecture), but the site URL now comes from the connection.

### 2.2 Return structured `destination_path` from agent

**Files:** `agent.py:171–174`, `SortController.cs:131`

Currently `result` contains LLM prose, but the API stores it as `ProcessedFile.NewPath`.

**Agent change** — capture the actual destination from tool calls and return it separately:
```python
results.append({
    "file": file_path,
    "status": "success",
    "result": actual_destination_path,    # server-relative URL
    "description": res['messages'][-1].content,  # LLM summary
    "message": None
})
```

**API change** — store the path and description in the right fields:
```csharp
NewPath = r.Status == "success" ? r.Result : null,       // actual path
ErrorMessage = r.Description ?? r.Message,                // LLM summary or error
```

**DTO change** — add `description` to `AgentFileResult`:
```csharp
[JsonPropertyName("description")]
public string? Description { get; set; }
```

### 2.3 Handle agent error status in the API ✅

**File:** `SortController.cs:117–132`

After deserializing the agent response, the status is checked before recording results:

```csharp
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
```

### 2.4 Add `message` to `AgentResponse` DTO ✅

**File:** `SortDtos.cs:31–32`

```csharp
[JsonPropertyName("message")]
public string? Message { get; set; }
```

The agent returns `message` on error responses and empty-folder responses. Without this property, the error detail is silently dropped.

---

## Phase 3 — Smaller Fixes

### 3.1 Use unique session IDs per sort request ✅

**File:** `SortController.cs:106`

Changed to `$"session-{Guid.NewGuid()}"`. Prevents state contamination between concurrent sorts.

### 3.2 Clarify `id` field semantics

**Files:** `SortController.cs:77`, `agent.py:132`

Rename `id` to `organization_id` in both payload and agent for clarity. The agent currently treats it as `user_id` for S3 log paths.

**API:**
```csharp
organization_id = connection.OrganizationId.ToString(),
```

**Agent:**
```python
org_id = payload.get('organization_id', payload.get('id', 'default'))
```

### 3.3 Report true `files_found` before capping

**File:** `agent.py:128,154–156,189`

```python
original_count = len(file_urls)
if len(file_urls) > MAX_FILES:
    file_urls = file_urls[:MAX_FILES]

# ...later...
return {
    "status": "success",
    "files_found": original_count,
    "files_sorted": succeeded,
    "files_capped_at": MAX_FILES if original_count > MAX_FILES else None,
    "results": results
}
```

### 3.4 Consistent per-file result fields

**File:** `agent.py:171–182`

Always include all fields in every result entry:
```python
# success
results.append({
    "file": file_path,
    "status": "success",
    "result": destination_path,
    "description": llm_summary,
    "message": None
})

# error
results.append({
    "file": file_path,
    "status": "error",
    "result": None,
    "description": None,
    "message": str(e)
})
```

### 3.5 Fix `SortingRecipe.Rules` default ✅

**File:** `SortingRecipe.cs:16`

Changed to `"[]"` to match the expected string-array format.

---

## Phase 4 — Cleanup

### 4.1 Decide on unused `SharePointConnection` credential fields

**File:** `SharePointConnection.cs:9–12`

`ClientId`, `Thumbprint`, `PrivateKeyPath`, `DriveId` exist on the entity but are unused.

With the multi-tenant single-app-registration approach:
- `ClientId`, `Thumbprint`, `PrivateKeyPath` → **remove**. The global app registration's credentials live in Secrets Manager, not per-connection.
- `DriveId` → keep if there's a future need to target specific document libraries; otherwise remove.

If per-connection credential support is ever needed, these can be re-added.

### 4.2 Evaluate `OAuthToken` entity

**File:** `OAuthToken.cs`

This entity stores encrypted access/refresh tokens per connection. With the current architecture (client credentials grant via MSAL), there are no refresh tokens — the agent acquires short-lived tokens on every invocation.

Options:
- **Keep** if you plan to add a delegated (user-context) OAuth flow in the future
- **Remove** if the client credentials flow is the permanent approach
- The `OAuthTokensController` is a raw CRUD controller that accepts encrypted byte arrays — it has no token management logic and is not used by anything

### 4.3 Remove `app.py` from the API repository

After the consent flow is properly integrated into the C# API (Phase 1), the Flask prototype is no longer needed. Delete `/Users/zachbagley/sorterra-api/app.py`.

---

## Summary

| Phase | Scope | Status |
|-------|-------|--------|
| **1** | Admin Consent Onboarding | ✅ Done — API consent endpoints, frontend consent flow, connection status lifecycle |
| **2** | Agent ↔ API Critical Fixes | Partial — API-side done (site_url, error handling, message DTO). Agent-side pending (structured destination_path). |
| **3** | Smaller Fixes | Partial — Done: unique session IDs, Rules default. Pending: `id` rename, true `files_found`, consistent result fields (all require agent changes). |
| **4** | Cleanup | Not started — remove unused entity fields, evaluate OAuthToken, delete `app.py` prototype |

Phase 2 and 3 remaining items require coordinating with Nathan on agent changes (we cannot modify sorterra-agent).
