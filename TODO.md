# Sorterra Backend TODO

This document tracks the remaining work for the Sorterra backend API, organized by sprint and priority.

## Current Status

### Completed
- [x] Project structure (Sorterra.Api, Sorterra.Core, Sorterra.Infrastructure)
- [x] Docker containerization (MySQL, API Dockerfile, docker-compose)
- [x] Database schema (11 tables with relationships and indexes)
- [x] Entity Framework Core DbContext with full model configuration
- [x] Domain entities (User, Organization, SharePointConnection, etc.)
- [x] Health check endpoints (`/health`, `/health/live`, `/health/ready`)
- [x] Serilog logging configuration
- [x] Swagger/OpenAPI documentation setup
- [x] Development seed data

### In Progress
- [ ] API controller implementation (see Sprint 1-2 below)

---

## Sprint 1: Connectivity & Foundation

### Authentication (High Priority)
- [ ] **BACKEND-002**: Configure Amazon Cognito User Pool
  - Create user pool in AWS Console
  - Configure app client with proper OAuth settings
  - Set up hosted UI for login flow
  - Document client IDs and endpoints in team wiki

- [ ] **BACKEND-003**: Implement Cognito JWT validation middleware
  - Add JWT Bearer authentication in `Program.cs`
  - Create `JwtValidationMiddleware.cs` to validate Cognito tokens
  - Extract user claims (sub, email, groups)
  - Implement `[Authorize]` attribute on protected endpoints
  - Create `CurrentUserService` to access authenticated user info

### SharePoint Integration (High Priority)
- [ ] **BACKEND-005**: Research Microsoft Graph API authentication
  - Register Azure AD application
  - Document required permissions (Files.ReadWrite.All, Sites.ReadWrite.All)
  - Understand OAuth 2.0 authorization code flow
  - Prototype token acquisition in a test project

- [ ] **BACKEND-006**: Create SharePoint connection endpoints
  ```
  POST   /api/connections           - Initiate OAuth flow
  GET    /api/connections/callback  - Handle OAuth redirect
  GET    /api/connections           - List connections for org
  DELETE /api/connections/{id}      - Disconnect SharePoint site
  ```
  - Implement `ConnectionsController.cs`
  - Create `TokenEncryptionService.cs` for secure token storage
  - Store encrypted tokens in `oauth_tokens` table

- [ ] **BACKEND-007**: Implement Graph API service wrapper
  - Create `GraphApiService.cs` with typed HttpClient
  - Implement automatic token refresh logic
  - Add retry policies with Polly for transient failures
  - Methods needed:
    - `GetDriveAsync()` - Get SharePoint drive info
    - `ListFilesAsync()` - List files in a folder
    - `GetFileContentAsync()` - Download file content
    - `MoveFileAsync()` - Move file to new location
    - `RenameFileAsync()` - Rename a file

---

## Sprint 2: Core Logic

### Webhook Integration
- [ ] **BACKEND-008**: Implement webhook subscription management
  - Create Graph API webhook subscription for file changes
  - Store subscription ID in `sharepoint_connections` table
  - Implement subscription renewal before expiration (max 4230 minutes)
  - Handle subscription validation endpoint

- [ ] **BACKEND-009**: Create webhook receiver endpoint
  ```
  POST /api/webhooks/sharepoint  - Receive Graph API notifications
  ```
  - Validate notification signature
  - Parse change notifications
  - Publish events to EventBridge/SQS for async processing
  - Store raw events in `webhook_events` table

### CRUD APIs
- [ ] **BACKEND-010**: Build activity log API
  ```
  GET /api/activity  - Paginated activity feed with filtering
  ```
  - Filter by activity type, date range, entity type
  - Support pagination (offset/limit or cursor-based)
  - Consider SignalR for real-time updates (future)

- [ ] **BACKEND-011**: Create sorting recipes CRUD API
  ```
  GET    /api/recipes      - List recipes for organization
  POST   /api/recipes      - Create new recipe
  GET    /api/recipes/{id} - Get recipe details
  PUT    /api/recipes/{id} - Update recipe
  DELETE /api/recipes/{id} - Delete recipe
  ```
  - Validate path templates (e.g., `/Finance/[Year]/[Month]/`)
  - Implement priority ordering for recipe evaluation
  - Create `RecipesController.cs`

- [ ] **BACKEND-012**: Implement processed files API
  ```
  GET /api/files      - List files with filtering/pagination
  GET /api/files/{id} - Get file details with processing history
  ```
  - Filter by status, date range, classification type
  - Include related recipe and connection info
  - Create `FilesController.cs`

---

## Sprint 3: Search & File Operations

### Search
- [ ] **BACKEND-013**: Create natural language search endpoint
  ```
  POST /api/search  - Search documents by query
  ```
  - Accept query text
  - Generate query embedding (coordinate with ML team)
  - Perform vector similarity search
  - Return results with relevance scores
  - Create `SearchController.cs`

- [ ] **BACKEND-015**: Add search analytics tracking
  - Log queries to `search_queries` table
  - Track latency and result counts
  - Record clicked results for relevance feedback

### File Operations
- [ ] **BACKEND-014**: Implement file move/rename via Graph API
  - Apply recipe destination path templates
  - Handle path template variables: `[Year]`, `[Month]`, `[Vendor]`, `[Date]`
  - Log operations to activity feed
  - Update `processed_files` table with new paths
  - Handle conflicts (duplicate names)

---

## Sprint 4: Polish & Security

### Security
- [ ] **BACKEND-016**: Security audit and hardening
  - Review all endpoints for proper authorization
  - Ensure organization-level data isolation
  - Implement rate limiting (consider AspNetCoreRateLimit)
  - Add request validation with FluentValidation
  - SQL injection prevention review (EF Core parameterizes by default)
  - Review CORS configuration for production

### Error Handling
- [ ] **BACKEND-017**: Graph API error handling
  - Handle 429 (rate limit) with exponential backoff
  - Handle 401/403 with token refresh
  - Map Graph API errors to user-friendly messages
  - Implement circuit breaker pattern with Polly

### Documentation
- [ ] **BACKEND-018**: Complete API documentation
  - Add XML comments to all controllers and DTOs
  - Configure Swagger to display comments
  - Add request/response examples
  - Document error responses and codes

### Performance
- [ ] **BACKEND-019**: Performance optimization
  - Add response caching for read-heavy endpoints
  - Review and optimize database queries
  - Add missing indexes based on query patterns
  - Consider Redis for session/cache (future)

---

## Infrastructure Tasks

### Docker
- [ ] **DOCKER-006**: Prepare production container configuration
  - Create `docker-compose.prod.yml` with production settings
  - Configure AWS CloudWatch logging
  - Set resource limits (CPU, memory)
  - Document AWS ECS task definition

### CI/CD (Coordinate with McKay)
- [ ] Set up GitHub Actions for:
  - Build and test on PR
  - Docker image build and push to ECR
  - Deployment to ECS Fargate

---

## Technical Debt

### Package Updates
- [ ] Monitor Pomelo.EntityFrameworkCore.MySql for EF Core 10 support
  - Currently using EF Core 9.0 for compatibility
  - Upgrade when Pomelo releases v10.x

### Code Quality
- [ ] Add unit tests for services
- [ ] Add integration tests for API endpoints
- [ ] Set up code coverage reporting
- [ ] Configure SonarQube or similar for code analysis

### Cleanup
- [ ] Remove old `sorterra-api/` folder (original scaffolding)
- [ ] Remove `sorterra-api.sln` (old solution file)

---

## Research Items

These topics need investigation before implementation:

| Topic | Questions to Answer |
|-------|---------------------|
| **Webhook lifecycle** | How to renew Graph API subscriptions before expiration? |
| **Token refresh** | Strategy when SharePoint token expires mid-processing? |
| **Error surfacing** | How should file processing failures appear in dashboard? |
| **Concurrency** | What if same file triggers multiple webhook events? |
| **Vector search** | MySQL 9.0 vectors vs external store (OpenSearch, Pinecone)? |

---

## Coordination Points

| Team Member | You Need From Them | They Need From You |
|-------------|-------------------|-------------------|
| **Patrick** | API endpoint specs for frontend | JSON response formats, error codes |
| **McKay** | AWS VPC, RDS, S3 buckets | Database connection strings, container specs |
| **Nate/Caleb** | Embedding model choice, vector dimensions | Database ready for embeddings, vector search strategy |

---

## API Endpoints Summary (Target)

| Method | Endpoint | Status |
|--------|----------|--------|
| GET | `/health` | Done |
| GET | `/health/live` | Done |
| GET | `/health/ready` | Done |
| POST | `/api/auth/login` | Planned |
| GET | `/api/connections` | Planned |
| POST | `/api/connections` | Planned |
| GET | `/api/connections/callback` | Planned |
| DELETE | `/api/connections/{id}` | Planned |
| GET | `/api/recipes` | Planned |
| POST | `/api/recipes` | Planned |
| GET | `/api/recipes/{id}` | Planned |
| PUT | `/api/recipes/{id}` | Planned |
| DELETE | `/api/recipes/{id}` | Planned |
| GET | `/api/files` | Planned |
| GET | `/api/files/{id}` | Planned |
| POST | `/api/search` | Planned |
| GET | `/api/activity` | Planned |
| POST | `/api/webhooks/sharepoint` | Planned |

---

## Quick Reference: File Locations

| What | Where |
|------|-------|
| Add new controller | `src/Sorterra.Api/Controllers/` |
| Add new entity | `src/Sorterra.Core/Entities/` |
| Add new service interface | `src/Sorterra.Core/Interfaces/` |
| Add new DTO | `src/Sorterra.Core/DTOs/` |
| Add new service implementation | `src/Sorterra.Infrastructure/Services/` |
| Add new repository | `src/Sorterra.Infrastructure/Repositories/` |
| Update DbContext | `src/Sorterra.Infrastructure/Data/SorterraDbContext.cs` |
| Update database schema | `docker/mysql/init/01-schema.sql` |
| Update seed data | `docker/mysql/init/02-seed-data.sql` |
