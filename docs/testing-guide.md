# Sorterra — Deployed Product Testing Guide

Step-by-step instructions for testing the deployed Sorterra application, including setting up a real SharePoint connection for end-to-end sort testing.

## Access

| Resource | URL |
|----------|-----|
| Frontend | `https://sorterra.app` |
| API | `https://sorterra.app/api/` |
| API Health | `https://sorterra.app/api/health` |

## Test Account

| Field | Value |
|-------|-------|
| Email | `zachaltaccs@gmail.com` |
| Password | `SortTest2026Ab#` |

This account was set up via Cognito `admin-set-user-password`. It is associated with the seed organization and has access to all seed data.

---

## 1. API Health Checks

Verify the API is running. No authentication required.

1. Open `https://sorterra.app/api/health` in a browser or curl
   - **Expected:** `{"status":"Healthy","timestamp":"...","checks":{"database":"Healthy"}}`
2. Open `https://sorterra.app/api/health/live`
   - **Expected:** `{"status":"Alive"}`

---

## 2. Authentication

### 2a. Login via Frontend

1. Open `https://sorterra.app`
   - **Expected:** Redirects to `/login`
2. Enter the test account credentials (see above)
3. Click **Sign In**
   - **Expected:** Redirects to `/dashboard`

### 2b. Login via API (for curl testing)

Generate a JWT token for API testing:

```bash
TOKEN=$(aws cognito-idp admin-initiate-auth \
  --user-pool-id us-east-1_d63e7X9x7 \
  --client-id 1ccr4hrojdp2kt96qohc2a05s5 \
  --auth-flow ADMIN_USER_PASSWORD_AUTH \
  --auth-parameters 'USERNAME=24e80498-a061-705b-a676-961316b736aa,PASSWORD=SortTest2026Ab#' \
  --region us-east-1 \
  --query "AuthenticationResult.AccessToken" \
  --output text)
```

Test it:

```bash
curl -s https://sorterra.app/api/sortingrecipes \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

**Expected:** JSON array of recipes (not a 401).

---

## 3. Dashboard

1. After logging in, you should land on `/dashboard`
   - **Expected:** Dashboard page loads with summary widgets
2. Check the sidebar navigation — all links should work:
   - **Dashboard** (`/dashboard`)
   - **Recipes** (`/recipes`)
   - **Files** (`/files`)
   - **Settings** (`/settings`)

---

## 4. Recipes Page

1. Navigate to `/recipes`
   - **Expected:** Displays seed recipes:
     - **Invoice Sorting** — "Automatically sort invoices by vendor and date"
     - **Contract Filing** — "Sort contracts by client and year"
2. Click a recipe to view its details
3. Try creating a new recipe:
   - Click **Add Recipe**
   - Fill in a name, description, and rules
   - Click **Save**
   - **Expected:** Recipe appears in the list
4. Try editing and deleting a recipe

---

## 5. Settings Page

### 5a. Profile Section

1. Navigate to `/settings`
   - **Expected:** Profile section shows the test user's display name and email
2. Edit the display name and click **Save Changes**
   - **Expected:** Success toast, changes persist on refresh

### 5b. Organization Section

1. Scroll to the Organization section
   - **Expected:** Shows the seed organization name and member list

### 5c. SharePoint Connections

1. Scroll to the SharePoint Connections section
   - **Expected:** One seed connection to `https://acmecorp.sharepoint.com/sites/Finance` with status **Active**
2. Try adding a new connection:
   - Click **Add Connection**
   - Enter a site URL (any URL for testing)
   - Click **Add Connection**
   - **Expected:** New connection appears with **Pending** status
3. Try deleting the test connection you just created

---

## 6. Set Up a Real SharePoint Connection

Follow these steps to create a real SharePoint site, register an Azure AD app, and add the connection to Sorterra so you can test end-to-end sorting.

### 6a. Get a Microsoft 365 Tenant

If you don't already have a Microsoft 365 tenant with SharePoint:

1. Go to https://developer.microsoft.com/en-us/microsoft-365/dev-program
2. Click **Join now** and sign up for the Microsoft 365 Developer Program
3. Set up your instant sandbox — this gives you a free E5 tenant with SharePoint
4. Note your admin credentials (e.g., `admin@yourtenant.onmicrosoft.com`)

If you already have a tenant, skip to 6b.

### 6b. Create a Test SharePoint Site with Files

1. Go to `https://yourtenant.sharepoint.com` (replace `yourtenant` with your actual tenant name)
2. Click **+ Create site** > **Team site**
   - Site name: `Sorterra Test`
   - Keep defaults, click **Create**
3. Once created, go to **Documents** (Shared Documents library)
4. Create a folder called `SortTest`
5. Upload 3–5 test files into the `SortTest` folder, for example:
   - `invoice-acme-jan2026.pdf`
   - `contract-globex-2026.docx`
   - `receipt-staples-feb2026.pdf`
   - `report-q1-2026.xlsx`
   - `memo-team-update.docx`
6. Note the full site URL — it will be something like:
   `https://yourtenant.sharepoint.com/sites/SorterraTest`

### 6c. Register an Azure AD App

The Sorterra agent authenticates to SharePoint using **certificate-based auth** (MSAL with a self-signed certificate), not a client secret. It uses the SharePoint REST API (not Microsoft Graph).

1. Go to https://entra.microsoft.com (or https://portal.azure.com > Microsoft Entra ID)
2. Navigate to **App registrations** > **+ New registration**
   - Name: `Sorterra Agent`
   - Supported account types: **Accounts in this organizational directory only** (single tenant)
   - Redirect URI: leave blank
   - Click **Register**
3. On the app's overview page, note these values:
   - **Application (client) ID** — e.g., `a1b2c3d4-e5f6-7890-abcd-ef1234567890`
   - **Directory (tenant) ID** — e.g., `f9e8d7c6-b5a4-3210-fedc-ba0987654321`

### 6d. Create a Self-Signed Certificate

The agent uses certificate-based authentication via MSAL. Generate a self-signed certificate and upload the public key to the app registration.

**Generate the certificate (on macOS/Linux):**

```bash
# Generate a self-signed certificate (valid for 1 year)
openssl req -x509 -newkey rsa:2048 -keyout sorterra-agent.key -out sorterra-agent.crt \
  -days 365 -nodes -subj "/CN=Sorterra Agent"

# Get the thumbprint (SHA-1, uppercase, no colons)
openssl x509 -in sorterra-agent.crt -fingerprint -noout \
  | sed 's/sha1 Fingerprint=//;s/://g'
```

Note the **thumbprint** value (e.g., `A1B2C3D4E5F6...`).

**Upload to Azure AD:**

1. In the app registration, go to **Certificates & secrets** > **Certificates** > **Upload certificate**
2. Upload `sorterra-agent.crt` (the public key only)
3. Click **Add**
4. Verify the thumbprint matches what you generated above

**Keep the private key file** (`sorterra-agent.key`) — Nathan will need its contents.

### 6e. Grant SharePoint API Permissions

The agent uses the SharePoint REST API, so it needs **SharePoint** application permissions (not Microsoft Graph).

1. In the app registration, go to **API permissions** > **+ Add a permission**
2. Select **SharePoint** (not Microsoft Graph) > **Application permissions**
3. Add this permission:
   - `Sites.FullControl.All` — full control of all site collections
4. Click **Add permissions**
5. Click **Grant admin consent for [your tenant]** (requires admin rights)
   - **Expected:** The permission shows a green checkmark under "Status"

> **Why FullControl?** The agent needs to read files, create folders, move files, rename files, and manage permissions. `Sites.FullControl.All` covers all of these operations.

### 6f. Summary of Values to Collect

After completing 6b–6e, you should have:

| Value | Example | Where to find it |
|-------|---------|-------------------|
| Tenant ID | `f9e8d7c6-b5a4-3210-fedc-ba0987654321` | Entra > App registration > Overview |
| Client ID | `a1b2c3d4-e5f6-7890-abcd-ef1234567890` | Entra > App registration > Overview |
| Certificate Thumbprint | `A1B2C3D4E5F6...` | From openssl command above (or Entra > Certificates & secrets) |
| Private Key (PEM) | Contents of `sorterra-agent.key` | The file generated by openssl |
| Site URL | `https://yourtenant.sharepoint.com/sites/SorterraTest` | SharePoint browser URL |
| Folder Path | `/sites/SorterraTest/Shared Documents/SortTest/` | Path within the SharePoint site |

### 6g. Coordinate with Nathan (Agent Credentials)

The Sorterra agent authenticates to SharePoint independently using credentials stored in **AWS Secrets Manager**. The SortController sends only `tenant_id`, `path`, and `recipe` to the agent — it does **not** forward client credentials.

The agent reads its credentials from a single secret: **`sorterra/tenants/tenant_a`** in AWS Secrets Manager (us-east-1). Nathan needs to update this secret with your app registration's credentials.

**Send Nathan these values:**
- **Tenant ID** — your Azure AD directory (tenant) ID
- **Client ID** — your app registration's application (client) ID
- **Certificate Thumbprint** — SHA-1 thumbprint from step 6d (uppercase, no colons)
- **Private Key** — the full PEM contents of `sorterra-agent.key`
- **Site URL** — your SharePoint site URL (e.g., `https://yourtenant.sharepoint.com/sites/SorterraTest`)

**Ask Nathan to update the Secrets Manager secret with this JSON structure:**

```json
{
  "TENANT_ID": "your-tenant-id",
  "CLIENT_ID": "your-client-id",
  "THUMBPRINT": "your-certificate-thumbprint",
  "PRIVATE_KEY": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----",
  "SHAREPOINT_SITE_URL": "https://yourtenant.sharepoint.com/sites/SorterraTest"
}
```

> **Important:** The `SHAREPOINT_SITE_URL` in the secret determines the authentication scope. The agent acquires a token scoped to `https://yourtenant.sharepoint.com/.default`, so this must match your tenant's SharePoint host.

**Ask Nathan to confirm:**
1. The secret has been updated in AWS Secrets Manager
2. The agent can successfully list files in your test SharePoint site
3. The expected folder path format (e.g., `/sites/SorterraTest/Shared Documents/SortTest/`)

Do not proceed to step 6h until Nathan confirms the agent can access your SharePoint.

### 6h. Create the Connection in Sorterra

Once Nathan confirms the agent has access, create the connection via the API:

```bash
# Get a token first (see section 2b)

curl -s -X POST https://sorterra.app/api/sharepointconnections \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "organizationId": "11111111-1111-1111-1111-111111111111",
    "siteUrl": "https://yourtenant.sharepoint.com/sites/SorterraTest",
    "tenantId": "YOUR_TENANT_ID",
    "clientId": "YOUR_CLIENT_ID",
    "sourceFolder": "/sites/SorterraTest/Shared Documents/SortTest/",
    "connectionStatus": "active"
  }' | python3 -m json.tool
```

**Save the `id` from the response** — you'll need it to trigger a sort.

Or create it via the frontend:
1. Go to **Settings** > **SharePoint Connections**
2. Click **Add Connection**
3. Enter the site URL and tenant ID
4. Click **Add Connection**
5. Then update the connection status to `active` via the API:
   ```bash
   curl -s -X PUT https://sorterra.app/api/sharepointconnections/CONNECTION_ID \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"connectionStatus": "active", "tenantId": "YOUR_TENANT_ID"}'
   ```

### 6i. Create a Recipe with String Array Rules

The agent expects rules as a JSON array of natural-language strings. The seed recipes use a different format, so create a new one:

```bash
curl -s -X POST https://sorterra.app/api/sortingrecipes \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "organizationId": "11111111-1111-1111-1111-111111111111",
    "name": "Test Sort Recipe",
    "description": "Sort test files by document type",
    "isActive": true,
    "priority": 1,
    "rules": "[\"Move invoices and receipts to Finance/\", \"Move contracts to Legal/\", \"Move reports and memos to General/\"]"
  }' | python3 -m json.tool
```

**Save the `id` from the response.**

> **Note:** The `rules` field is stored as a string in the database. The value must be a JSON-encoded string array — a string containing a JSON array, not the array itself. That's why there are escaped quotes in the example above.

Or create it via the frontend:
1. Go to **Recipes** > **Add Recipe**
2. Name: `Test Sort Recipe`
3. Rules: `["Move invoices and receipts to Finance/", "Move contracts to Legal/", "Move reports and memos to General/"]`
4. Click **Save**

---

## 7. End-to-End Sort Test

### 7a. Sort via Frontend

1. Go to **Settings**
2. Find your real SharePoint connection card
3. Click **Sort Now**
4. In the modal:
   - **Recipe:** Select "Test Sort Recipe"
   - **Folder path:** `/sites/SorterraTest/Shared Documents/SortTest/`
5. Click **Sort Now**
6. **Expected (with real SharePoint):**
   - Loading spinner with "Sorting files... this may take a minute"
   - After 10–60 seconds, results appear:
     - Green summary: "Sorted X/Y files"
     - Per-file results showing where each file was moved
   - Success toast notification
7. Click **View Files** to see the ProcessedFile records

### 7b. Sort via curl

```bash
curl -s -X POST https://sorterra.app/api/sort \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "connectionId": "YOUR_CONNECTION_ID",
    "recipeId": "YOUR_RECIPE_ID",
    "folderPath": "/sites/SorterraTest/Shared Documents/SortTest/"
  }' | python3 -m json.tool
```

**Expected success response:**

```json
{
  "status": "success",
  "filesFound": 5,
  "filesSorted": 4,
  "connectionId": "...",
  "recipeId": "...",
  "results": [
    {
      "file": "/sites/SorterraTest/Shared Documents/SortTest/invoice-acme-jan2026.pdf",
      "status": "success",
      "result": "Moved to Finance/",
      "message": null,
      "processedFileId": "..."
    }
  ]
}
```

### 7c. Verify Database Records

After a successful sort:

```bash
# Processed files should now have entries
curl -s https://sorterra.app/api/processedfiles \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Activity log should have a new sort_completed entry
curl -s https://sorterra.app/api/activitylogs \
  -H "Authorization: Bearer $TOKEN" | python3 -c "
import sys, json
for l in json.load(sys.stdin):
    if l['activityType'] == 'sort_completed':
        print(f'{l[\"createdAt\"]} — {l[\"description\"]}')"

# Recipe filesProcessedCount should have incremented
curl -s https://sorterra.app/api/sortingrecipes/YOUR_RECIPE_ID \
  -H "Authorization: Bearer $TOKEN" | python3 -c "
import sys, json
r = json.load(sys.stdin)
print(f'{r[\"name\"]}: filesProcessedCount = {r[\"filesProcessedCount\"]}')"
```

### 7d. Verify in SharePoint

Go back to your SharePoint site and check that the files were actually moved to the folders specified by the recipe rules (e.g., `Finance/`, `Legal/`, `General/`).

---

## 8. Sort API — Error Cases (via curl)

These verify the backend validation:

```bash
# 404 — bad connection ID
curl -s -X POST https://sorterra.app/api/sort \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"connectionId":"00000000-0000-0000-0000-000000000000","recipeId":"33333333-3333-3333-3333-333333333333","folderPath":"/test/"}' \
  | python3 -m json.tool
# Expected: {"error": "Connection not found"}

# 404 — bad recipe ID
curl -s -X POST https://sorterra.app/api/sort \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"connectionId":"96b93da9-4e76-47f7-a9f5-fd67ae7df3ea","recipeId":"00000000-0000-0000-0000-000000000000","folderPath":"/test/"}' \
  | python3 -m json.tool
# Expected: {"error": "Recipe not found or inactive"}

# 401 — no token
curl -s -o /dev/null -w "%{http_code}" -X POST https://sorterra.app/api/sort
# Expected: 401
```

---

## 9. Files Page

1. Navigate to `/files`
   - **Expected:** Lists ProcessedFile records from sort operations
   - Before any real sort: empty or showing records from test sorts with seed data
   - After a real sort (section 7): files appear with status, original path, and new path
2. Verify each file entry shows the correct recipe and connection association

---

## 10. Activity Logs (API only)

Activity logs are not exposed in the frontend UI yet, but can be verified via the API:

```bash
curl -s https://sorterra.app/api/activitylogs \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

After running a sort, look for an entry with:
- `activityType: "sort_completed"`
- `description: "Sorted X/Y files in /path/..."`

---

## 11. Logout

1. Click the logout button in the sidebar or header
   - **Expected:** Redirects to `/login`, token is cleared

---

## Seed Data Reference

The database has the following seed records:

| Entity | Key Fields |
|--------|-----------|
| Organization | `11111111-1111-1111-1111-111111111111` — seed org |
| User | `zachaltaccs@gmail.com` (Cognito sub: `24e80498-a061-705b-a676-961316b736aa`) |
| SharePoint Connection | `96b93da9-...` — `acmecorp.sharepoint.com/sites/Finance`, tenant `acme-tenant-001`, status `active` (fake) |
| Recipe: Invoice Sorting | `33333333-...` — active, priority 10 (rules in JSON object format — not compatible with agent) |
| Recipe: Contract Filing | `e5cf8b9f-...` — active, priority 20 (rules in JSON object format — not compatible with agent) |

---

## Known Limitations

- **Sort timeout:** Large folder sorts may exceed the ALB timeout (60s), returning a 504. Future work: async processing with polling.
- **Recipe rules format:** Seed recipes store rules as JSON objects, but the agent expects a string array (`["rule1", "rule2"]`). New recipes should use the array format. The backend falls back gracefully but the agent may not understand the old format.
- **Agent credentials:** The API sends `tenant_id`, `path`, and `recipe` to the agent but not SharePoint credentials. The agent reads its own credentials (client ID, certificate thumbprint, private key PEM) from AWS Secrets Manager (`sorterra/tenants/tenant_a`). Currently a single-tenant setup — testing with a different tenant requires Nathan to update the secret.
- **Agent file cap:** The agent processes a maximum of 10 files per sort invocation (safety cap in agent code).
- **Activity logs:** Only accessible via API, no frontend page yet.
- **Connection modal:** The frontend Add Connection modal only captures site URL, tenant ID, and source folder. The client ID and other fields must be set via the API.
