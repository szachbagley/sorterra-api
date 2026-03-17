# Sorterra API Reference

**Base URL**: `http://35.175.101.240`

The API follows REST conventions. All request and response bodies use JSON (`Content-Type: application/json`). IDs are UUIDs. Timestamps are UTC in ISO 8601 format.

## Common Response Codes

| Code | Meaning |
|------|---------|
| 200  | Success (GET, PUT) |
| 201  | Created (POST) — includes the new resource in the body |
| 204  | No Content (DELETE) |
| 404  | Resource not found |
| 503  | Service unavailable (health check only) |

---

## Health

### GET /health

Returns overall API and database health.

```json
{
  "status": "Healthy",
  "timestamp": "2026-02-06T22:10:59.0606342Z",
  "checks": {
    "database": "Healthy"
  }
}
```

### GET /health/ready

Readiness probe — is the app ready to receive traffic?

```json
{
  "status": "Ready"
}
```

Returns 503 if the database is unreachable.

### GET /health/live

Liveness probe — is the process running?

```json
{
  "status": "Alive"
}
```

---

## Users

### GET /api/users

Returns all users.

```json
[
  {
    "id": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "cognitoSub": "cognito-sub-sarah",
    "email": "sarah.chen@acmecorp.com",
    "displayName": "Sarah Chen",
    "createdAt": "2026-02-06T21:57:53",
    "updatedAt": "2026-02-06T21:57:53",
    "lastLoginAt": null
  }
]
```

### GET /api/users/{id}

Returns a single user by ID.

### POST /api/users

Create a user.

**Request body:**

| Field | Type | Required |
|-------|------|----------|
| cognitoSub | string | yes |
| email | string | yes |
| displayName | string | no |

```json
{
  "cognitoSub": "cognito-sub-123",
  "email": "jane@example.com",
  "displayName": "Jane Doe"
}
```

**Response (201):** The created user object.

### PUT /api/users/{id}

Update a user. All fields are optional — only include the fields you want to change.

| Field | Type |
|-------|------|
| email | string |
| displayName | string |
| lastLoginAt | datetime |

### DELETE /api/users/{id}

Delete a user. Returns 204 on success.

---

## Organizations

### GET /api/organizations

Returns all organizations.

```json
[
  {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "name": "Acme Corp",
    "createdAt": "2026-02-06T21:57:53",
    "settings": "{\"plan\": \"professional\"}"
  }
]
```

### GET /api/organizations/{id}

Returns a single organization.

### POST /api/organizations

Create an organization.

| Field | Type | Required |
|-------|------|----------|
| name | string | yes |
| settings | string (JSON) | no |

### PUT /api/organizations/{id}

Update an organization. All fields optional.

| Field | Type |
|-------|------|
| name | string |
| settings | string (JSON) |

### DELETE /api/organizations/{id}

Delete an organization. Returns 204.

---

## User Organizations

Manages the many-to-many relationship between users and organizations.

### GET /api/userorganizations

Returns all memberships.

```json
[
  {
    "userId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "organizationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "role": "owner",
    "joinedAt": "2026-02-06T21:57:53"
  }
]
```

### GET /api/userorganizations/{userId}/{organizationId}

Returns a single membership by its composite key.

### POST /api/userorganizations

Create a membership.

| Field | Type | Required |
|-------|------|----------|
| userId | uuid | yes |
| organizationId | uuid | yes |
| role | string | no (defaults to `member`) |

### PUT /api/userorganizations/{userId}/{organizationId}

Update a membership.

| Field | Type |
|-------|------|
| role | string |

### DELETE /api/userorganizations/{userId}/{organizationId}

Delete a membership. Returns 204.

---

## SharePoint Connections

### GET /api/sharepointconnections

Returns all connections.

```json
[
  {
    "id": "44444444-4444-4444-4444-444444444444",
    "organizationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "siteUrl": "https://acmecorp.sharepoint.com/sites/Finance",
    "tenantId": "acme-tenant-001",
    "clientId": "app-client-id-001",
    "thumbprint": "ABC123DEF456...",
    "privateKeyPath": "/certs/sharepoint-app.pem",
    "driveId": "drive-finance-001",
    "sourceFolder": "/Unsorted",
    "connectionStatus": "active",
    "lastSyncAt": null,
    "webhookSubscriptionId": null,
    "webhookExpiration": null,
    "createdBy": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "createdAt": "2026-02-06T21:57:53",
    "updatedAt": "2026-02-06T21:57:53",
    "errorMessage": null
  }
]
```

### GET /api/sharepointconnections/{id}

Returns a single connection.

### POST /api/sharepointconnections

Create a connection.

| Field | Type | Required |
|-------|------|----------|
| organizationId | uuid | yes |
| siteUrl | string | yes |
| tenantId | string | no |
| clientId | string | no |
| thumbprint | string | no |
| privateKeyPath | string | no |
| driveId | string | no |
| sourceFolder | string | no |
| connectionStatus | string | no (defaults to `pending`) |
| createdBy | uuid | no |

### PUT /api/sharepointconnections/{id}

Update a connection. All fields optional.

| Field | Type |
|-------|------|
| siteUrl | string |
| tenantId | string |
| clientId | string |
| thumbprint | string |
| privateKeyPath | string |
| driveId | string |
| sourceFolder | string |
| connectionStatus | string |
| lastSyncAt | datetime |
| webhookSubscriptionId | string |
| webhookExpiration | datetime |
| errorMessage | string |

### DELETE /api/sharepointconnections/{id}

Delete a connection. Returns 204.

---

## Sorting Recipes

### GET /api/sortingrecipes

Returns all sorting recipes. Supports optional query parameters:

| Parameter | Type | Description |
|-----------|------|-------------|
| organizationId | uuid | Filter by organization |
| isActive | bool | Filter by active status |
| orderBy | string | `name`, `createdat`, or `priority` (default) |

Example: `GET /api/sortingrecipes?organizationId=aaaa...&isActive=true&orderBy=name`

### GET /api/sortingrecipes/{id}

Returns a single recipe.

### GET /api/sortingrecipes/by-connection/{connectionId}

Returns all **active** recipes for the organization that owns the given SharePoint connection, sorted by priority. Returns 404 if the connection doesn't exist.

```json
[
  {
    "id": "77777777-7777-7777-7777-777777777777",
    "organizationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "name": "Invoice Sorting",
    "description": "Sort invoices by vendor and date",
    "fileTypePattern": "Invoice",
    "destinationPathTemplate": "/Finance/Invoices/[Year]/[Month]/",
    "isActive": true,
    "priority": 10,
    "createdBy": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "createdAt": "2026-02-06T21:57:53",
    "updatedAt": "2026-02-06T21:57:53",
    "rules": "{\"actions\": {\"extract_fields\": [\"vendor\", \"date\", \"amount\"], \"rename_pattern\": \"[Vendor]_Invoice_[Date]\"}, \"conditions\": [{\"field\": \"content_type\", \"value\": \"invoice\", \"operator\": \"equals\"}]}",
    "filesProcessedCount": 0
  }
]
```

### POST /api/sortingrecipes

Create a recipe.

| Field | Type | Required |
|-------|------|----------|
| organizationId | uuid | yes |
| name | string | yes |
| description | string | no |
| fileTypePattern | string | no |
| destinationPathTemplate | string | no |
| isActive | bool | no (defaults to `true`) |
| priority | int | no (defaults to `0`) |
| createdBy | uuid | no |
| rules | string (JSON) | no |

### PUT /api/sortingrecipes/{id}

Update a recipe. All fields optional.

| Field | Type |
|-------|------|
| name | string |
| description | string |
| fileTypePattern | string |
| destinationPathTemplate | string |
| isActive | bool |
| priority | int |
| rules | string (JSON) |

### DELETE /api/sortingrecipes/{id}

Delete a recipe. Returns 204.

---

## Processed Files

### GET /api/processedfiles

Returns all processed files.

```json
[
  {
    "id": "uuid",
    "organizationId": "uuid",
    "connectionId": "uuid",
    "sharePointItemId": "string",
    "sharePointDriveId": "string",
    "originalName": "Q1-Report.pdf",
    "newName": "Acme_Invoice_2026-01.pdf",
    "originalPath": "/Unsorted/Q1-Report.pdf",
    "newPath": "/Finance/Invoices/2026/January/Acme_Invoice_2026-01.pdf",
    "fileExtension": ".pdf",
    "fileSizeBytes": 204800,
    "mimeType": "application/pdf",
    "classifiedType": "invoice",
    "classificationConfidence": 0.95,
    "appliedRecipeId": "uuid",
    "status": "completed",
    "processedAt": "2026-02-06T22:00:00",
    "errorMessage": null,
    "extractedMetadata": "{\"vendor\": \"Acme\", \"amount\": 1500.00}",
    "createdAt": "2026-02-06T21:58:00"
  }
]
```

### GET /api/processedfiles/{id}

Returns a single processed file.

### POST /api/processedfiles

Create a processed file record.

| Field | Type | Required |
|-------|------|----------|
| organizationId | uuid | yes |
| sharePointItemId | string | yes |
| originalName | string | yes |
| connectionId | uuid | no |
| sharePointDriveId | string | no |
| originalPath | string | no |
| fileExtension | string | no |
| fileSizeBytes | long | no |
| mimeType | string | no |
| classifiedType | string | no |
| classificationConfidence | decimal | no |
| appliedRecipeId | uuid | no |
| status | string | no (defaults to `pending`) |
| extractedMetadata | string (JSON) | no |

### PUT /api/processedfiles/{id}

Update a processed file. All fields optional.

| Field | Type |
|-------|------|
| newName | string |
| newPath | string |
| classifiedType | string |
| classificationConfidence | decimal |
| appliedRecipeId | uuid |
| status | string |
| processedAt | datetime |
| errorMessage | string |
| extractedMetadata | string (JSON) |

### DELETE /api/processedfiles/{id}

Delete a processed file. Returns 204.

---

## Document Chunks

### GET /api/documentchunks

Returns all document chunks.

```json
[
  {
    "id": "uuid",
    "processedFileId": "uuid",
    "organizationId": "uuid",
    "chunkIndex": 0,
    "chunkText": "Section of document text...",
    "chunkTokens": 128,
    "embedding": null,
    "pageNumber": 1,
    "sectionHeader": "Introduction",
    "createdAt": "2026-02-06T22:00:00"
  }
]
```

### GET /api/documentchunks/{id}

Returns a single chunk.

### POST /api/documentchunks

Create a chunk.

| Field | Type | Required |
|-------|------|----------|
| processedFileId | uuid | yes |
| organizationId | uuid | yes |
| chunkIndex | int | yes |
| chunkText | string | yes |
| chunkTokens | int | no |
| embedding | string | no |
| pageNumber | int | no |
| sectionHeader | string | no |

### PUT /api/documentchunks/{id}

Update a chunk. All fields optional.

| Field | Type |
|-------|------|
| chunkText | string |
| chunkTokens | int |
| embedding | string |
| pageNumber | int |
| sectionHeader | string |

### DELETE /api/documentchunks/{id}

Delete a chunk. Returns 204.

---

## Activity Logs

### GET /api/activitylogs

Returns all activity logs.

```json
[
  {
    "id": "uuid",
    "organizationId": "uuid",
    "userId": "uuid",
    "activityType": "file_sorted",
    "entityType": "processed_file",
    "entityId": "uuid",
    "description": "Sorted Q1-Report.pdf to /Finance/Invoices/",
    "metadata": "{\"recipe\": \"Invoice Sorting\"}",
    "createdAt": "2026-02-06T22:00:00"
  }
]
```

### GET /api/activitylogs/{id}

Returns a single log entry.

### POST /api/activitylogs

Create a log entry.

| Field | Type | Required |
|-------|------|----------|
| organizationId | uuid | yes |
| activityType | string | yes |
| userId | uuid | no |
| entityType | string | no |
| entityId | uuid | no |
| description | string | no |
| metadata | string (JSON) | no |

### PUT /api/activitylogs/{id}

Update a log entry. All fields optional.

| Field | Type |
|-------|------|
| description | string |
| metadata | string (JSON) |

### DELETE /api/activitylogs/{id}

Delete a log entry. Returns 204.

---

## Search Queries

### GET /api/searchqueries

Returns all search queries.

```json
[
  {
    "id": "uuid",
    "organizationId": "uuid",
    "userId": "uuid",
    "queryText": "Q1 invoices",
    "queryEmbedding": null,
    "resultsCount": 5,
    "latencyMs": 120,
    "clickedResultIds": null,
    "createdAt": "2026-02-06T22:00:00"
  }
]
```

### GET /api/searchqueries/{id}

Returns a single query record.

### POST /api/searchqueries

Create a query record.

| Field | Type | Required |
|-------|------|----------|
| organizationId | uuid | yes |
| queryText | string | yes |
| userId | uuid | no |
| queryEmbedding | string | no |
| resultsCount | int | no |
| latencyMs | int | no |
| clickedResultIds | string | no |

### PUT /api/searchqueries/{id}

Update a query record. All fields optional.

| Field | Type |
|-------|------|
| resultsCount | int |
| latencyMs | int |
| clickedResultIds | string |

### DELETE /api/searchqueries/{id}

Delete a query record. Returns 204.

---

## OAuth Tokens

### GET /api/oauthtokens

Returns all tokens. Encrypted token values are **not** included in responses.

```json
[
  {
    "id": "uuid",
    "connectionId": "uuid",
    "tokenType": "Bearer",
    "expiresAt": "2026-02-07T22:00:00",
    "scope": "Files.ReadWrite.All",
    "createdAt": "2026-02-06T22:00:00",
    "updatedAt": "2026-02-06T22:00:00"
  }
]
```

### GET /api/oauthtokens/{id}

Returns a single token.

### POST /api/oauthtokens

Create a token.

| Field | Type | Required |
|-------|------|----------|
| connectionId | uuid | yes |
| accessTokenEncrypted | byte[] | yes |
| refreshTokenEncrypted | byte[] | yes |
| expiresAt | datetime | yes |
| tokenType | string | no (defaults to `Bearer`) |
| scope | string | no |

### PUT /api/oauthtokens/{id}

Update a token. All fields optional.

| Field | Type |
|-------|------|
| accessTokenEncrypted | byte[] |
| refreshTokenEncrypted | byte[] |
| tokenType | string |
| expiresAt | datetime |
| scope | string |

### DELETE /api/oauthtokens/{id}

Delete a token. Returns 204.

---

## Webhook Events

### GET /api/webhookevents

Returns all webhook events.

```json
[
  {
    "id": "uuid",
    "connectionId": "uuid",
    "eventType": "updated",
    "resourceType": "driveItem",
    "resourceId": "item-abc-123",
    "rawPayload": "{...}",
    "processingStatus": "received",
    "processedAt": null,
    "errorMessage": null,
    "receivedAt": "2026-02-06T22:00:00"
  }
]
```

### GET /api/webhookevents/{id}

Returns a single event.

### POST /api/webhookevents

Create an event.

| Field | Type | Required |
|-------|------|----------|
| connectionId | uuid | yes |
| rawPayload | string | yes |
| eventType | string | no |
| resourceType | string | no |
| resourceId | string | no |
| processingStatus | string | no (defaults to `received`) |

### PUT /api/webhookevents/{id}

Update an event. All fields optional.

| Field | Type |
|-------|------|
| processingStatus | string |
| processedAt | datetime |
| errorMessage | string |

### DELETE /api/webhookevents/{id}

Delete an event. Returns 204.

---

## Sort

### POST /api/sort

Triggers file sorting on a SharePoint connection. Merges all active recipes for the connection's organization into a single agent invocation via Bedrock AgentCore. Records results as `ProcessedFile` records and creates an `ActivityLog` entry.

**Requires:** JWT authentication.

**Request body:**

| Field | Type | Required |
|-------|------|----------|
| connectionId | uuid | yes |
| recipeId | uuid | yes (but all active recipes are merged) |
| folderPath | string | yes |

```json
{
  "connectionId": "44444444-4444-4444-4444-444444444444",
  "recipeId": "77777777-7777-7777-7777-777777777777",
  "folderPath": "/sites/Sorterra/Shared Documents/Inbox/"
}
```

**Response (200):**

```json
{
  "status": "success",
  "filesFound": 5,
  "filesSorted": 4,
  "connectionId": "44444444-...",
  "results": [
    {
      "file": "/sites/.../invoice.pdf",
      "status": "success",
      "result": "Moved to Finance/Invoices/",
      "message": null,
      "processedFileId": "..."
    }
  ]
}
```

**Error responses:**

| Code | Condition |
|------|-----------|
| 400 | Connection missing TenantId, or no active recipes / no rules defined |
| 404 | Connection or recipe not found |
| 502 | Agent returned an error, permission denied, or agent not found |

---

## SharePoint Auth (Admin Consent)

### GET /api/auth/sharepoint/consent

Returns the Azure AD admin consent URL for a pending SharePoint connection. The frontend should redirect the browser to this URL.

**Requires:** JWT authentication.

**Query parameters:**

| Parameter | Type | Required |
|-----------|------|----------|
| connectionId | uuid | yes |

**Response (200):**

```json
{
  "consentUrl": "https://login.microsoftonline.com/common/adminconsent?client_id=...&state=...&redirect_uri=..."
}
```

**Error responses:**

| Code | Condition |
|------|-----------|
| 400 | Connection is not in `pending` status |
| 404 | Connection not found |
| 500 | AzureAd configuration missing |

### GET /api/auth/sharepoint/callback

Handles Microsoft's redirect after admin consent. **Not authenticated** — called by Microsoft's browser redirect. Validates the signed state JWT, captures the tenant ID, updates the connection to `consented` status, and redirects the browser to the frontend.

**Query parameters (from Microsoft):**

| Parameter | Description |
|-----------|-------------|
| state | Signed JWT state token |
| tenant | The consenting organization's Azure AD tenant ID |
| admin_consent | `"True"` if consent was granted |
| error | Error code (if consent failed) |
| error_description | Error details (if consent failed) |

**Response:** 302 redirect to `{FrontendBaseUrl}/settings?consent=success` or `?consent=error&message=...`
