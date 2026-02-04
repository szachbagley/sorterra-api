# Agent Recipe Access

How the AI file-sorting agent retrieves sorting recipes from the Sorterra API.

## Overview

When a file is uploaded or modified in a connected SharePoint site, the agent receives a webhook event containing the `connectionId` of the SharePoint connection. The agent uses this ID to fetch the active sorting recipes for that connection's organization, then evaluates each recipe against the file to determine how it should be classified and moved.

## Endpoint

```
GET /api/sortingrecipes/by-connection/{connectionId}
```

Returns all **active** sorting recipes for the organization that owns the given SharePoint connection, ordered by **priority ascending** (lowest number = highest priority).

### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `connectionId` | GUID | The SharePoint connection ID from the webhook event |

### Response

**200 OK** -- Array of recipes (may be empty if no recipes are configured):

```json
[
  {
    "id": "77777777-7777-7777-7777-777777777777",
    "organizationId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "name": "Contract Filing",
    "description": "Sort contracts by client and year",
    "fileTypePattern": "Contract",
    "destinationPathTemplate": "/Legal/Contracts/[Year]/[Client]/",
    "isActive": true,
    "priority": 20,
    "createdBy": "cccccccc-cccc-cccc-cccc-cccccccccccc",
    "createdAt": "2026-01-22T15:11:45",
    "updatedAt": "2026-01-22T15:11:45",
    "rules": "{\"conditions\": [{\"field\": \"content_type\", \"operator\": \"equals\", \"value\": \"contract\"}], \"actions\": {\"rename_pattern\": \"[Client]_Contract_[Date]\", \"extract_fields\": [\"client\", \"date\", \"value\", \"expiration\"]}}",
    "filesProcessedCount": 34
  }
]
```

**404 Not Found** -- The connection ID does not exist:

```json
{
  "error": "Connection not found",
  "connectionId": "00000000-0000-0000-0000-000000000000"
}
```

## Agent Workflow

```
1. Receive webhook event with connectionId
2. GET /api/sortingrecipes/by-connection/{connectionId}
3. Iterate recipes in order (already sorted by priority)
4. For each recipe:
   a. Match fileTypePattern against the file's AI classification
   b. Evaluate rules JSON conditions against file metadata
   c. If match: apply destinationPathTemplate to generate the target path
   d. Stop at first match
5. If no recipe matches, mark the file as unmatched
```

## Recipe Fields Reference

| Field | Type | Agent Usage |
|-------|------|-------------|
| `fileTypePattern` | string | Match against the file's AI-classified type (e.g., "Invoice", "Contract") |
| `rules` | JSON string | Conditions to evaluate against file metadata (see Rules Format below) |
| `destinationPathTemplate` | string | Path template with variables to resolve (e.g., `/Finance/[Year]/[Month]/`) |
| `priority` | int | Evaluation order. Lower = higher priority. Recipes are pre-sorted by this field |
| `isActive` | bool | Always `true` in responses from this endpoint (inactive recipes are filtered out) |

## Rules Format

The `rules` field is a JSON string with `conditions` and `actions`:

```json
{
  "conditions": [
    {
      "field": "content_type",
      "operator": "equals",
      "value": "invoice"
    },
    {
      "field": "file_extension",
      "operator": "in",
      "value": [".pdf", ".docx"]
    }
  ],
  "actions": {
    "rename_pattern": "[Vendor]_Invoice_[Date]",
    "extract_fields": ["vendor", "date", "amount"]
  }
}
```

### Condition Operators

| Operator | Description | Value Type |
|----------|-------------|------------|
| `equals` | Exact match | string |
| `contains` | Substring match | string |
| `starts_with` | Prefix match | string |
| `in` | Match any in list | array of strings |

### Template Variables

Used in `destinationPathTemplate` and `rename_pattern`:

| Variable | Source |
|----------|--------|
| `[Year]` | Extracted from file metadata or current date |
| `[Month]` | Extracted from file metadata or current date |
| `[Date]` | Full date from file content |
| `[Vendor]` | Extracted vendor/company name |
| `[Client]` | Extracted client name |
| `[Department]` | Extracted department |
| `[DocumentType]` | AI classification result |
| `[CaseNumber]` | Extracted case number (legal docs) |

## Alternative: Filtered List Endpoint

For cases where the agent already knows the organization ID, or needs more control over filtering:

```
GET /api/sortingrecipes?organizationId={orgId}&isActive=true&orderBy=priority
```

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `organizationId` | GUID | _(none)_ | Filter by organization |
| `isActive` | bool | _(none)_ | Filter by active status |
| `orderBy` | string | `priority` | Sort by: `priority`, `name`, or `createdAt` |

All parameters are optional. Omitting all parameters returns every recipe across all organizations.

## Example

```bash
# Agent receives webhook for connection 44444444-4444-4444-4444-444444444444
curl http://localhost:5001/api/sortingrecipes/by-connection/44444444-4444-4444-4444-444444444444
```

```json
[
  {
    "name": "Meeting Notes Archive",
    "priority": 5,
    "fileTypePattern": "Meeting Notes",
    "destinationPathTemplate": "/Archive/Meetings/[Department]/[Year]-[Month]/",
    "rules": "{\"conditions\": [{\"field\": \"content_type\", \"operator\": \"contains\", \"value\": \"meeting\"}], ...}"
  },
  {
    "name": "HR Document Sorting",
    "priority": 15,
    "fileTypePattern": "HR",
    "destinationPathTemplate": "/HR/[DocumentType]/[Year]/",
    "rules": "{\"conditions\": [{\"field\": \"source_folder\", \"operator\": \"starts_with\", \"value\": \"/Uploads/HR\"}], ...}"
  },
  {
    "name": "Contract Filing",
    "priority": 20,
    "fileTypePattern": "Contract",
    "destinationPathTemplate": "/Legal/Contracts/[Year]/[Client]/",
    "rules": "{\"conditions\": [{\"field\": \"content_type\", \"operator\": \"equals\", \"value\": \"contract\"}], ...}"
  }
]
```
