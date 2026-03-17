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
- [x] DTOs for all 11 entities (Create, Update, Response records in `Sorterra.Core/DTOs/`)
- [x] CRUD controllers for all 11 database tables (GET all, GET by ID, POST, PUT, DELETE)
- [x] Dockerfile updated for .NET 10 runtime compatibility
- [x] Docker environment verified (MySQL, API, Adminer containers healthy)
- [x] Sample data populated across all 11 tables (multiple orgs, users, connections, recipes, processed files, etc.)
- [x] Sorting recipes query filtering (`?organizationId`, `?isActive`, `?orderBy`)
- [x] Agent recipe lookup endpoint (`GET /api/sortingrecipes/by-connection/{connectionId}`)
- [x] Agent recipe access documentation (`docs/agent-recipe-access.md`)
- [x] Documentation moved to `docs/` directory
- [x] API reference documentation (`docs/api-reference.md`)
- [x] AWS EC2 deployment guide (`docs/aws-ec2-deployment.md`)
- [x] AWS ECS Fargate deployment guide (`docs/aws-ecs-fargate-deployment.md`)
- [x] AWS infrastructure documentation (`docs/aws-infrastructure.md`)
- [x] ECS Fargate deployment (API + MySQL containers live on AWS)
- [x] ECR repositories for API and MySQL images
- [x] Application Load Balancer with health checks
- [x] EFS persistent storage for MySQL
- [x] Cloud Map service discovery (`sorterra.local` namespace)
- [x] CloudWatch logging for ECS containers
- [x] IAM execution and task roles for ECS

- [x] Amazon Cognito JWT validation middleware (JWT Bearer auth in `Program.cs`)
- [x] `[Authorize]` attribute on all protected controllers
- [x] `CurrentUserService` to access authenticated user info
- [x] Sort endpoint (`POST /api/sort`) with Bedrock AgentCore agent integration
- [x] `SortController` with connection lookup, recipe merging, agent invocation, result recording
- [x] `SortDtos` (request, agent response, frontend response models)
- [x] Azure AD admin consent flow (`SharePointAuthController` + `ConsentStateService`)
- [x] Frontend admin consent integration (ConnectionModal, Settings callback handling)

### In Progress
- [ ] Azure AD app registration prerequisites (see `docs/integration-plan.md`)

---

## Sprint 1: Connectivity & Foundation

### Authentication (High Priority) ✅
- [x] **BACKEND-002**: Configure Amazon Cognito User Pool
  - User pool created: `us-east-1_d63e7X9x7`
  - App client configured: `1ccr4hrojdp2kt96qohc2a05s5`
  - See `docs/aws-cognito-setup.md`

- [x] **BACKEND-003**: Implement Cognito JWT validation middleware
  - JWT Bearer authentication configured in `Program.cs`
  - `[Authorize]` attribute on all protected controllers
  - `CurrentUserService` extracts authenticated user claims

### SharePoint Integration (High Priority) — Partially Done
- [x] **BACKEND-005**: Azure AD admin consent flow
  - App registration client ID: `120d6fc7-f18a-4507-ad34-6ae1d41bd0db`
  - Multi-tenant admin consent via `login.microsoftonline.com/common/adminconsent`
  - The agent authenticates to SharePoint via certificate-based MSAL (not Graph API directly from the API)
  - See `docs/integration-plan.md`

- [x] **BACKEND-006**: SharePoint auth endpoints
  - `GET /api/auth/sharepoint/consent` — Returns consent URL for pending connections
  - `GET /api/auth/sharepoint/callback` — Handles Microsoft redirect, captures tenant ID
  - `SharePointAuthController.cs` + `ConsentStateService.cs`
  - SharePoint connection CRUD already in `SharePointConnectionsController.cs`

- [ ] **BACKEND-007**: Graph API service wrapper (deprioritized)
  - File operations are handled by the agent (Python) via SharePoint REST API, not the C# API
  - May revisit if direct API-to-SharePoint operations are needed in the future

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
- [x] **BACKEND-010**: Build activity log API (basic CRUD implemented)
  - [x] CRUD endpoints at `/api/activitylogs`
  - [ ] Add pagination (offset/limit or cursor-based)
  - [ ] Add filtering by activity type, date range, entity type
  - [ ] Consider SignalR for real-time updates (future)

- [x] **BACKEND-011**: Create sorting recipes CRUD API (basic CRUD implemented)
  - [x] CRUD endpoints at `/api/sortingrecipes`
  - [x] Implement priority ordering for recipe evaluation
  - [x] Add query filtering by organizationId, isActive, orderBy
  - [x] Add `GET /api/sortingrecipes/by-connection/{connectionId}` for agent access
  - [ ] Validate path templates (e.g., `/Finance/[Year]/[Month]/`)

- [x] **BACKEND-012**: Implement processed files API (basic CRUD implemented)
  - [x] CRUD endpoints at `/api/processedfiles`
  - [ ] Add filtering by status, date range, classification type
  - [ ] Add pagination
  - [ ] Include related recipe and connection info in responses

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
  - Move `Encryption:TokenEncryptionKey` from `appsettings.json` to AWS Secrets Manager or ECS task definition environment variables before production deployment (coordinate with McKay)
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

### Docker & Deployment
- [x] **DOCKER-006**: Production container configuration
  - [x] ECS Fargate task definitions for API (256 CPU / 512 MB) and MySQL (512 CPU / 1024 MB)
  - [x] AWS CloudWatch logging (`/ecs/sorterra-api`, `/ecs/sorterra-mysql`)
  - [x] Resource limits configured in task definitions
  - [x] ALB with health check routing
  - [x] EFS volume for MySQL data persistence
  - [x] Cloud Map service discovery for inter-container networking

### CI/CD (Coordinate with McKay)
- [ ] Set up GitHub Actions for:
  - Build and test on PR
  - Docker image build and push to ECR
  - Automated deployment to ECS Fargate

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
| **McKay** | CI/CD pipeline, production domain/SSL | ECS task definitions, deployment docs |
| **Nate/Caleb** | Embedding model choice, vector dimensions | Database ready for embeddings, vector search strategy |

---

## API Endpoints Summary

### Health Checks

| Method | Endpoint | Status |
|--------|----------|--------|
| GET | `/health` | Done |
| GET | `/health/live` | Done |
| GET | `/health/ready` | Done |

### CRUD Endpoints (all tables)

Each resource has GET (list), GET `{id}`, POST, PUT `{id}`, DELETE `{id}`.

| Resource | Route | Status |
|----------|-------|--------|
| Users | `/api/users` | Done |
| Organizations | `/api/organizations` | Done |
| User Organizations | `/api/userorganizations` | Done (composite key: `{userId}/{organizationId}`) |
| SharePoint Connections | `/api/sharepointconnections` | Done |
| OAuth Tokens | `/api/oauthtokens` | Done (response excludes encrypted fields) |
| Sorting Recipes | `/api/sortingrecipes` | Done (+ filtering, `by-connection` endpoint) |
| Processed Files | `/api/processedfiles` | Done |
| Document Chunks | `/api/documentchunks` | Done |
| Activity Logs | `/api/activitylogs` | Done |
| Webhook Events | `/api/webhookevents` | Done |
| Search Queries | `/api/searchqueries` | Done |

### Sort Endpoint

| Method | Endpoint | Status |
|--------|----------|--------|
| POST | `/api/sort` | Done |

### SharePoint Auth (Admin Consent)

| Method | Endpoint | Status |
|--------|----------|--------|
| GET | `/api/auth/sharepoint/consent` | Done |
| GET | `/api/auth/sharepoint/callback` | Done |

### Agent Endpoints

| Method | Endpoint | Status |
|--------|----------|--------|
| GET | `/api/sortingrecipes/by-connection/{connectionId}` | Done |

### Planned Endpoints

| Method | Endpoint | Status |
|--------|----------|--------|
| POST | `/api/search` | Planned (semantic search) |
| POST | `/api/webhooks/sharepoint` | Planned (Graph API webhook receiver) |

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
