# AWS Cognito Setup Guide

Set up Amazon Cognito as the authentication provider for the Sorterra API. This covers creating the User Pool, configuring the app client, and integrating JWT validation into the API.

## Overview

```
Frontend (React)                    AWS Cognito                     Sorterra API
     │                                  │                                │
     │  1. Redirect to Hosted UI  ────► │                                │
     │                                  │                                │
     │  ◄──── 2. User logs in ────────► │                                │
     │                                  │                                │
     │  ◄── 3. Redirect with code ───── │                                │
     │                                  │                                │
     │  4. Exchange code for tokens ──► │                                │
     │                                  │                                │
     │  ◄── 5. ID + Access tokens ───── │                                │
     │                                  │                                │
     │  6. API request + Bearer token ──┼──────────────────────────────► │
     │                                  │                                │
     │                                  │  ◄── 7. Validate JWT ────────► │
     │                                  │      (JWKS public keys)        │
     │                                  │                                │
     │  ◄──────────────────────────────────── 8. Response ────────────── │
```

**Auth flow**: Authorization Code Grant with PKCE (recommended for SPAs).

**Tokens**:
- **ID Token**: Contains user identity claims (`sub`, `email`, `name`). Used by the API to identify the user.
- **Access Token**: Authorizes API access. Sent in the `Authorization: Bearer` header.
- **Refresh Token**: Used by the frontend to get new tokens without re-login.

## Prerequisites

- AWS CLI authenticated to account `896170900648` in `us-east-1`
- The frontend callback URL (ask Patrick for the exact domain; use `http://localhost:3000` for local dev)

Set environment variables:

```bash
export AWS_REGION=us-east-1
```

## 1. Create the Cognito User Pool

The User Pool stores user accounts and handles authentication.

```bash
export USER_POOL_ID=$(aws cognito-idp create-user-pool \
  --pool-name sorterra-dev \
  --auto-verified-attributes email \
  --username-attributes email \
  --username-configuration CaseSensitive=false \
  --policies '{
    "PasswordPolicy": {
      "MinimumLength": 8,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireNumbers": true,
      "RequireSymbols": false,
      "TemporaryPasswordValidityDays": 7
    }
  }' \
  --schema '[
    {"Name": "email", "Required": true, "Mutable": true},
    {"Name": "name", "Required": false, "Mutable": true}
  ]' \
  --mfa-configuration OFF \
  --account-recovery-setting '{
    "RecoveryMechanisms": [
      {"Priority": 1, "Name": "verified_email"}
    ]
  }' \
  --query "UserPool.Id" --output text --region $AWS_REGION)

echo "User Pool ID: $USER_POOL_ID"
```

Key choices:
- **Username = email**: Users sign in with their email address
- **Auto-verify email**: Cognito sends a verification code on sign-up
- **MFA off**: Simplifies testing; enable for production later
- **No symbol requirement**: Reduces friction during testing

Tag the pool:

```bash
aws cognito-idp tag-resource \
  --resource-arn arn:aws:cognito-idp:$AWS_REGION:896170900648:userpool/$USER_POOL_ID \
  --tags Project=Sorterra,Environment=Dev \
  --region $AWS_REGION
```

## 2. Configure the Hosted UI Domain

Cognito's Hosted UI provides a pre-built login page. This avoids building login forms from scratch.

```bash
aws cognito-idp create-user-pool-domain \
  --domain sorterra-dev \
  --user-pool-id $USER_POOL_ID \
  --region $AWS_REGION
```

The Hosted UI will be available at:
```
https://sorterra-dev.auth.us-east-1.amazoncognito.com
```

> If the domain name is taken, try `sorterra-dev-<random>` (e.g., `sorterra-dev-2026`).

## 3. Create the App Client

The app client is what the frontend uses to interact with Cognito. It defines the OAuth flow, callback URLs, and token settings.

```bash
export APP_CLIENT_ID=$(aws cognito-idp create-user-pool-client \
  --user-pool-id $USER_POOL_ID \
  --client-name sorterra-web \
  --generate-secret false \
  --explicit-auth-flows ALLOW_USER_SRP_AUTH ALLOW_REFRESH_TOKEN_AUTH \
  --supported-identity-providers COGNITO \
  --callback-urls '["http://localhost:3000/auth/callback"]' \
  --logout-urls '["http://localhost:3000"]' \
  --allowed-o-auth-flows code \
  --allowed-o-auth-scopes openid email profile \
  --allowed-o-auth-flows-user-pool-client true \
  --access-token-validity 1 \
  --id-token-validity 1 \
  --refresh-token-validity 30 \
  --token-validity-units '{
    "AccessToken": "hours",
    "IdToken": "hours",
    "RefreshToken": "days"
  }' \
  --prevent-user-existence-errors ENABLED \
  --query "UserPoolClient.ClientId" --output text --region $AWS_REGION)

echo "App Client ID: $APP_CLIENT_ID"
```

Key choices:
- **No client secret**: Required for public clients (SPAs). The secret can't be stored safely in browser code.
- **Authorization Code flow**: Most secure for SPAs (with PKCE, handled by the frontend SDK).
- **Scopes**: `openid` (required), `email` (get email in token), `profile` (get name in token).
- **Token lifetime**: 1-hour access/ID tokens, 30-day refresh tokens.
- **Callback URL**: `http://localhost:3000/auth/callback` for local dev. Add the production URL later (see [Section 8](#8-adding-production-callback-urls)).

## 4. Create a Test User

Create a user for immediate testing without going through the sign-up flow:

```bash
# Create user
aws cognito-idp admin-create-user \
  --user-pool-id $USER_POOL_ID \
  --username sarah.chen@acmecorp.com \
  --user-attributes \
    Name=email,Value=sarah.chen@acmecorp.com \
    Name=email_verified,Value=true \
    Name=name,Value="Sarah Chen" \
  --message-action SUPPRESS \
  --region $AWS_REGION

# Set a permanent password (bypasses forced password change)
aws cognito-idp admin-set-user-password \
  --user-pool-id $USER_POOL_ID \
  --username sarah.chen@acmecorp.com \
  --password "Sorterra2026!" \
  --permanent \
  --region $AWS_REGION

echo "Test user created: sarah.chen@acmecorp.com / Sorterra2026!"
```

> This matches the seed data user in the database. The `cognito_sub` for this user will be set when they first authenticate and the API links their Cognito identity to the database record.

## 5. Record the Configuration Values

Print all the values needed for the API and frontend:

```bash
echo "============================================"
echo "Cognito Configuration"
echo "============================================"
echo "Region:        $AWS_REGION"
echo "User Pool ID:  $USER_POOL_ID"
echo "App Client ID: $APP_CLIENT_ID"
echo "Authority:     https://cognito-idp.$AWS_REGION.amazonaws.com/$USER_POOL_ID"
echo "Hosted UI:     https://sorterra-dev.auth.$AWS_REGION.amazoncognito.com"
echo "JWKS URL:      https://cognito-idp.$AWS_REGION.amazonaws.com/$USER_POOL_ID/.well-known/jwks.json"
echo "============================================"
```

Save these values. You'll need them for the API configuration and to share with the frontend team.

## 6. Update the API Configuration

### appsettings.json

Update the Cognito section in `src/Sorterra.Api/appsettings.json`:

```json
"Cognito": {
  "Region": "us-east-1",
  "UserPoolId": "<USER_POOL_ID>",
  "AppClientId": "<APP_CLIENT_ID>",
  "Authority": "https://cognito-idp.us-east-1.amazonaws.com/<USER_POOL_ID>"
}
```

### ECS Task Definition

Update the API task definition environment variables to include the Cognito values:

```json
{"name": "Cognito__Region", "value": "us-east-1"},
{"name": "Cognito__UserPoolId", "value": "<USER_POOL_ID>"},
{"name": "Cognito__AppClientId", "value": "<APP_CLIENT_ID>"},
{"name": "Cognito__Authority", "value": "https://cognito-idp.us-east-1.amazonaws.com/<USER_POOL_ID>"}
```

Then register the updated task definition and force a new deployment (see [aws-ecs-update-redeployment.md](aws-ecs-update-redeployment.md)).

## 7. Integrate JWT Validation into the API

This section describes the code changes needed in the API to validate Cognito JWTs. These correspond to TODO items **BACKEND-003**.

### 7a. Add Authentication to Program.cs

Add JWT Bearer authentication to the service configuration in `src/Sorterra.Api/Program.cs`:

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// ... existing code ...

// Authentication — Cognito JWT validation
var cognitoAuthority = builder.Configuration["Cognito:Authority"];
var cognitoClientId = builder.Configuration["Cognito:AppClientId"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = cognitoAuthority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cognitoAuthority,
            ValidateAudience = true,
            ValidAudience = cognitoClientId,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();
```

Then add `app.UseAuthentication()` **before** `app.UseAuthorization()` in the middleware pipeline:

```csharp
app.UseCors();
app.UseAuthentication();   // <-- add this line
app.UseAuthorization();
app.MapControllers();
```

### 7b. Protect Endpoints with [Authorize]

Add the `[Authorize]` attribute to controllers that require authentication:

```csharp
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize]                    // <-- requires valid JWT
public class UsersController : ControllerBase
{
    // ...
}
```

Leave these endpoints **open** (no `[Authorize]`):
- `HealthController` — ALB/NLB health checks must work without auth
- Webhook endpoints (if added later) — external services call these

### 7c. Access User Claims in Controllers

The authenticated user's Cognito `sub` and email are available in the JWT claims:

```csharp
// Inside any [Authorize] controller method:
var cognitoSub = User.FindFirst("sub")?.Value;
var email = User.FindFirst("email")?.Value;
```

### 7d. Create CurrentUserService (Optional)

For cleaner code, create a service that provides the authenticated user's info. This is useful if you need to look up the database user from their Cognito sub.

`src/Sorterra.Core/Interfaces/ICurrentUserService.cs`:
```csharp
public interface ICurrentUserService
{
    string? CognitoSub { get; }
    string? Email { get; }
}
```

`src/Sorterra.Api/Services/CurrentUserService.cs`:
```csharp
using System.Security.Claims;
using Sorterra.Core.Interfaces;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CognitoSub =>
        _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string? Email =>
        _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
```

### 7e. Update Swagger for Auth Testing

Update the Swagger configuration to include a JWT input field so you can test authenticated endpoints from the Swagger UI:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Sorterra API",
        Version = "v1",
        Description = "AI-powered file management system for SharePoint"
    });

    // Add JWT auth support to Swagger UI
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your Cognito JWT token"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
```

## 8. Adding Production Callback URLs

When the frontend is deployed, add the production callback URL to the app client:

```bash
aws cognito-idp update-user-pool-client \
  --user-pool-id $USER_POOL_ID \
  --client-id $APP_CLIENT_ID \
  --callback-urls '["http://localhost:3000/auth/callback", "https://your-production-domain.com/auth/callback"]' \
  --logout-urls '["http://localhost:3000", "https://your-production-domain.com"]' \
  --supported-identity-providers COGNITO \
  --allowed-o-auth-flows code \
  --allowed-o-auth-scopes openid email profile \
  --allowed-o-auth-flows-user-pool-client true \
  --region $AWS_REGION
```

> You must re-specify all OAuth settings when updating the client — the CLI replaces the entire config, it doesn't merge.

## 9. Testing Authentication

### Get a Token via Hosted UI

1. Open the Hosted UI login URL in a browser:

```
https://sorterra-dev.auth.us-east-1.amazoncognito.com/login?client_id=<APP_CLIENT_ID>&response_type=code&scope=openid+email+profile&redirect_uri=http://localhost:3000/auth/callback
```

2. Log in with the test user (`sarah.chen@acmecorp.com` / `Sorterra2026!`)
3. You'll be redirected to `http://localhost:3000/auth/callback?code=<AUTH_CODE>`
4. Exchange the code for tokens (the frontend SDK handles this automatically)

### Get a Token via CLI (for API testing)

Use the `admin-initiate-auth` command to get tokens directly without a browser:

```bash
TOKEN_RESPONSE=$(aws cognito-idp admin-initiate-auth \
  --user-pool-id $USER_POOL_ID \
  --client-id $APP_CLIENT_ID \
  --auth-flow ADMIN_USER_PASSWORD_AUTH \
  --auth-parameters USERNAME=sarah.chen@acmecorp.com,PASSWORD=Sorterra2026! \
  --region $AWS_REGION)

# Extract the ID token
ID_TOKEN=$(echo $TOKEN_RESPONSE | python3 -c "import sys,json; print(json.load(sys.stdin)['AuthenticationResult']['IdToken'])")

echo "ID Token: ${ID_TOKEN:0:50}..."
```

> **Note**: `ADMIN_USER_PASSWORD_AUTH` must be enabled on the app client for this to work. If not already enabled:
> ```bash
> aws cognito-idp update-user-pool-client \
>   --user-pool-id $USER_POOL_ID \
>   --client-id $APP_CLIENT_ID \
>   --explicit-auth-flows ALLOW_USER_SRP_AUTH ALLOW_REFRESH_TOKEN_AUTH ALLOW_ADMIN_USER_PASSWORD_AUTH \
>   --supported-identity-providers COGNITO \
>   --allowed-o-auth-flows code \
>   --allowed-o-auth-scopes openid email profile \
>   --allowed-o-auth-flows-user-pool-client true \
>   --region $AWS_REGION
> ```

### Test Authenticated Endpoints

```bash
# Should return 200 (with valid token)
curl -s -H "Authorization: Bearer $ID_TOKEN" http://35.175.101.240/api/users

# Should return 401 (no token)
curl -s -w "\nHTTP %{http_code}\n" http://35.175.101.240/api/users

# Should return 401 (invalid token)
curl -s -w "\nHTTP %{http_code}\n" -H "Authorization: Bearer invalid-token" http://35.175.101.240/api/users
```

### Health endpoints should work without auth

```bash
# These should always return 200, no token needed
curl -s http://35.175.101.240/health
curl -s http://35.175.101.240/health/live
curl -s http://35.175.101.240/health/ready
```

## 10. Information for the Frontend Team (Patrick)

Share this section with Patrick for frontend integration.

### Cognito Configuration

| Setting | Value |
|---------|-------|
| Region | `us-east-1` |
| User Pool ID | `<USER_POOL_ID>` |
| App Client ID | `<APP_CLIENT_ID>` |
| Hosted UI Domain | `https://sorterra-dev.auth.us-east-1.amazoncognito.com` |
| Redirect URI | `http://localhost:3000/auth/callback` |
| OAuth Flow | Authorization Code with PKCE |
| Scopes | `openid email profile` |

### Recommended Frontend Libraries

- **React**: Use [aws-amplify](https://docs.amplify.aws/react/build-a-backend/auth/) or [@aws-sdk/client-cognito-identity-provider](https://www.npmjs.com/package/@aws-sdk/client-cognito-identity-provider)
- Amplify handles the full OAuth flow (login, token refresh, session management) with minimal code

### API Request Format

```javascript
// Include the ID token in the Authorization header
const response = await fetch('http://35.175.101.240/api/users', {
  headers: {
    'Authorization': `Bearer ${idToken}`,
    'Content-Type': 'application/json'
  }
});
```

### Token Refresh

- Access and ID tokens expire after 1 hour
- Use the refresh token to get new tokens without re-login
- Amplify handles this automatically
- Refresh tokens expire after 30 days (user must re-login)

### Test Credentials

| Email | Password |
|-------|----------|
| `sarah.chen@acmecorp.com` | `Sorterra2026!` |

## Cleanup

To delete all Cognito resources:

```bash
# Delete app client
aws cognito-idp delete-user-pool-client \
  --user-pool-id $USER_POOL_ID \
  --client-id $APP_CLIENT_ID \
  --region $AWS_REGION

# Delete hosted UI domain
aws cognito-idp delete-user-pool-domain \
  --domain sorterra-dev \
  --user-pool-id $USER_POOL_ID \
  --region $AWS_REGION

# Delete user pool (deletes all users)
aws cognito-idp delete-user-pool \
  --user-pool-id $USER_POOL_ID \
  --region $AWS_REGION
```

## Cost

Cognito pricing for the User Pool:
- **Free tier**: 50,000 monthly active users (MAU) — more than enough for dev/testing
- **Beyond free tier**: $0.0055 per MAU
- The Hosted UI and token endpoints are included at no extra cost

## Files to Modify (Code Changes)

| File | Change |
|------|--------|
| `src/Sorterra.Api/Program.cs` | Add JWT Bearer auth, `UseAuthentication()`, Swagger auth |
| `src/Sorterra.Api/appsettings.json` | Fill in Cognito config values |
| `src/Sorterra.Api/Controllers/*Controller.cs` | Add `[Authorize]` to protected controllers |
| `src/Sorterra.Core/Interfaces/ICurrentUserService.cs` | New file — user context interface |
| `src/Sorterra.Api/Services/CurrentUserService.cs` | New file — extract claims from JWT |

## Checklist

- [ ] Create Cognito User Pool (`aws cognito-idp create-user-pool`)
- [ ] Set up Hosted UI domain (`aws cognito-idp create-user-pool-domain`)
- [ ] Create app client (`aws cognito-idp create-user-pool-client`)
- [ ] Create test user (`aws cognito-idp admin-create-user`)
- [ ] Update `appsettings.json` with Cognito values
- [ ] Add JWT Bearer authentication to `Program.cs`
- [ ] Add `UseAuthentication()` to middleware pipeline
- [ ] Add `[Authorize]` to protected controllers
- [ ] Update Swagger with Bearer token support
- [ ] Update ECS task definition with Cognito env vars
- [ ] Redeploy API to ECS
- [ ] Test: authenticated request returns 200
- [ ] Test: unauthenticated request returns 401
- [ ] Test: health endpoints work without auth
- [ ] Share config values with Patrick (frontend)
