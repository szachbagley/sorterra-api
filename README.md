# Sorterra API

Sorterra is an AI-powered file management system that integrates with SharePoint to automatically classify, sort, and organize documents. This repository contains the backend .NET API.

## Overview

Sorterra solves the problem of disorganized cloud storage by:
- **Auto-Classifying** files using AI/ML (e.g., "Invoice", "Contract", "Meeting Minutes")
- **Auto-Sorting** files into standardized folder structures based on user-defined rules ("Recipes")
- **RAG Indexing** for semantic search across document content
- **Natural Language Queries** allowing users to ask questions about their documents

## Tech Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 |
| Web Framework | ASP.NET Core |
| Database | MySQL 8.0 |
| ORM | Entity Framework Core 9.0 + Pomelo MySQL |
| Authentication | Amazon Cognito (planned) |
| Deployment | AWS ECS Fargate |
| External API | Microsoft Graph API for SharePoint |
| Logging | Serilog |
| API Documentation | Swagger/OpenAPI |
| Containerization | Docker + Docker Compose |

## Project Structure

```
sorterra-api/
├── Sorterra.sln                           # Solution file
├── src/
│   ├── Sorterra.Api/                      # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   ├── HealthController.cs        # Health check endpoints
│   │   │   ├── UsersController.cs         # User CRUD
│   │   │   ├── OrganizationsController.cs # Organization CRUD
│   │   │   ├── UserOrganizationsController.cs
│   │   │   ├── SharePointConnectionsController.cs
│   │   │   ├── OAuthTokensController.cs
│   │   │   ├── SortingRecipesController.cs
│   │   │   ├── ProcessedFilesController.cs
│   │   │   ├── DocumentChunksController.cs
│   │   │   ├── ActivityLogsController.cs
│   │   │   ├── WebhookEventsController.cs
│   │   │   └── SearchQueriesController.cs
│   │   ├── Middleware/                    # (placeholder for JWT middleware)
│   │   ├── Program.cs                     # Application entry point
│   │   ├── appsettings.json               # Production configuration
│   │   ├── appsettings.Development.json   # Development configuration
│   │   └── Sorterra.Api.csproj
│   │
│   ├── Sorterra.Core/                     # Domain models & interfaces
│   │   ├── Entities/                      # Domain entity classes
│   │   │   ├── User.cs
│   │   │   ├── Organization.cs
│   │   │   ├── UserOrganization.cs
│   │   │   ├── SharePointConnection.cs
│   │   │   ├── OAuthToken.cs
│   │   │   ├── SortingRecipe.cs
│   │   │   ├── ProcessedFile.cs
│   │   │   ├── DocumentChunk.cs
│   │   │   ├── ActivityLog.cs
│   │   │   ├── WebhookEvent.cs
│   │   │   └── SearchQuery.cs
│   │   ├── DTOs/                          # Request/response data transfer objects
│   │   │   ├── UserDtos.cs
│   │   │   ├── OrganizationDtos.cs
│   │   │   ├── UserOrganizationDtos.cs
│   │   │   ├── SharePointConnectionDtos.cs
│   │   │   ├── OAuthTokenDtos.cs
│   │   │   ├── SortingRecipeDtos.cs
│   │   │   ├── ProcessedFileDtos.cs
│   │   │   ├── DocumentChunkDtos.cs
│   │   │   ├── ActivityLogDtos.cs
│   │   │   ├── WebhookEventDtos.cs
│   │   │   └── SearchQueryDtos.cs
│   │   ├── Interfaces/                    # (placeholder for service interfaces)
│   │   └── Sorterra.Core.csproj
│   │
│   └── Sorterra.Infrastructure/           # External service implementations
│       ├── Data/
│       │   ├── SorterraDbContext.cs       # EF Core database context
│       │   └── Migrations/                # (placeholder for EF migrations)
│       ├── Services/                      # (placeholder for GraphApiService, etc.)
│       ├── Repositories/                  # (placeholder for data repositories)
│       └── Sorterra.Infrastructure.csproj
│
├── docs/
│   ├── TODO.md                            # Sprint backlog and task tracking
│   ├── agent-recipe-access.md             # How the AI agent retrieves sorting recipes
│   ├── api-reference.md                   # Full API reference documentation
│   ├── aws-ec2-deployment.md              # ECR + EC2 deployment guide (legacy)
│   ├── aws-ecs-fargate-deployment.md      # ECR + ECS Fargate deployment guide (current)
│   ├── aws-ecs-update-redeployment.md     # How to redeploy after code/schema changes
│   └── aws-infrastructure.md              # AWS infrastructure diagram and reference
│
├── tests/                                 # (placeholder for test projects)
│   ├── Sorterra.Api.Tests/
│   └── Sorterra.Infrastructure.Tests/
│
├── docker/
│   ├── api/
│   │   ├── Dockerfile                     # Multi-stage API build
│   │   └── .dockerignore
│   ├── mysql/
│   │   ├── Dockerfile                     # Custom MySQL image
│   │   ├── conf/my.cnf                    # MySQL configuration
│   │   └── init/
│   │       ├── 01-schema.sql              # Database schema
│   │       └── 02-seed-data.sql           # Development seed data
│   ├── docker-compose.yml                 # Development environment
│   ├── .env.example                       # Environment template
│   └── .env                               # Local environment (git-ignored)
│
├── .gitignore
├── .dockerignore
└── README.md
```

## Database Schema

The database supports multi-tenant file management with the following tables:

### Core Tables
| Table | Description |
|-------|-------------|
| `users` | User accounts synced with Cognito |
| `organizations` | Tenant/organization accounts |
| `user_organizations` | User-organization membership (many-to-many) |

### SharePoint Integration
| Table | Description |
|-------|-------------|
| `sharepoint_connections` | Connected SharePoint sites per organization (stores certificate auth credentials and source folder) |
| `oauth_tokens` | Encrypted OAuth tokens for SharePoint access |

### File Processing
| Table | Description |
|-------|-------------|
| `sorting_recipes` | User-defined sorting rules with conditions and actions |
| `processed_files` | Log of all files processed by the system |
| `document_chunks` | Text chunks with vector embeddings for RAG search |

### Activity & Analytics
| Table | Description |
|-------|-------------|
| `activity_log` | Audit trail for dashboard activity feed |
| `webhook_events` | Graph API webhook events for debugging/replay |
| `search_queries` | Search analytics and result tracking |

### Entity Relationships

```
User ←→ UserOrganization ←→ Organization
                              ↓
              ┌───────────────┼───────────────┐
              ↓               ↓               ↓
    SharePointConnection  SortingRecipe  ProcessedFile
              ↓                               ↓
         OAuthToken                    DocumentChunk
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Git

### Quick Start

1. **Clone the repository**
   ```bash
   cd sorterra-api
   ```

2. **Start the database**
   ```bash
   cd docker
   cp .env.example .env  # If not already done
   docker compose up -d mysql adminer
   ```

3. **Wait for MySQL to be healthy**
   ```bash
   docker ps  # Check STATUS shows "(healthy)"
   ```

4. **Run the API**
   ```bash
   cd ..
   ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Sorterra.Api
   ```

5. **Verify it's working**
   ```bash
   curl http://localhost:5000/health
   # Should return: {"status":"Healthy","timestamp":"...","checks":{"database":"Healthy"}}
   ```

### Available Endpoints

#### Health Checks

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Full health check with database status |
| `/health/live` | GET | Liveness probe (is the app running?) |
| `/health/ready` | GET | Readiness probe (is the app ready for traffic?) |
| `/swagger` | GET | Swagger UI for API documentation |

#### CRUD APIs

All CRUD endpoints follow REST conventions and use DTOs for request/response contracts. Each resource supports GET (list all), GET by ID, POST (create), PUT (update), and DELETE.

| Resource | Base Route | Notes |
|----------|-----------|-------|
| Users | `/api/users` | |
| Organizations | `/api/organizations` | |
| User Organizations | `/api/userorganizations` | Composite key: `{userId}/{organizationId}` |
| SharePoint Connections | `/api/sharepointconnections` | |
| OAuth Tokens | `/api/oauthtokens` | Response excludes encrypted token fields |
| Sorting Recipes | `/api/sortingrecipes` | Supports `?organizationId`, `?isActive`, `?orderBy` filtering |
| Processed Files | `/api/processedfiles` | |
| Document Chunks | `/api/documentchunks` | |
| Activity Logs | `/api/activitylogs` | |
| Webhook Events | `/api/webhookevents` | |
| Search Queries | `/api/searchqueries` | |

**Standard operations per resource:**

| Method | Route | Description | Status Code |
|--------|-------|-------------|-------------|
| GET | `/api/{resource}` | List all | 200 |
| GET | `/api/{resource}/{id}` | Get by ID | 200 / 404 |
| POST | `/api/{resource}` | Create | 201 + Location header |
| PUT | `/api/{resource}/{id}` | Update (partial) | 200 / 404 |
| DELETE | `/api/{resource}/{id}` | Delete | 204 / 404 |

**UserOrganizations** uses composite key routes instead of `{id}`:
`GET|PUT|DELETE /api/userorganizations/{userId}/{organizationId}`

#### Agent Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/sortingrecipes/by-connection/{connectionId}` | GET | Get active recipes for a connection's organization, ordered by priority |

See [`docs/agent-recipe-access.md`](docs/agent-recipe-access.md) for full details on how the AI file-sorting agent uses this endpoint.

### Service URLs (Development)

| Service | URL |
|---------|-----|
| Sorterra API | http://localhost:5001 |
| Swagger UI | http://localhost:5001/swagger |
| Adminer (DB UI) | http://localhost:8081 |
| MySQL | localhost:3307 |

Host ports are configurable via environment variables in `docker/.env`:
`API_HOST_PORT` (default: 5001), `MYSQL_HOST_PORT` (default: 3307).

### Database Credentials (Development)

| Setting | Value |
|---------|-------|
| Host | localhost |
| Port | 3307 |
| Database | sorterra_dev |
| User | sorterra |
| Password | sorterra_pass |
| Root Password | localdev |

## Docker Commands

### Starting Services
```bash
cd docker

# Start all services (MySQL, API, Adminer)
docker compose up -d

# Start only database services
docker compose up -d mysql adminer

# Start with rebuild
docker compose up -d --build
```

### Stopping Services
```bash
# Stop all services
docker compose down

# Stop and remove volumes (WARNING: deletes database data)
docker compose down -v
```

### Viewing Logs
```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f mysql
docker compose logs -f api
```

### Database Operations
```bash
# Connect to MySQL CLI
docker exec -it sorterra-mysql mysql -u sorterra -psorterra_pass sorterra_dev

# Run a SQL script
docker exec -i sorterra-mysql mysql -u sorterra -psorterra_pass sorterra_dev < script.sql

# Backup database
docker exec sorterra-mysql mysqldump -u root -plocaldev sorterra_dev > backup.sql

# Restore database
docker exec -i sorterra-mysql mysql -u root -plocaldev sorterra_dev < backup.sql
```

## Configuration

### Environment Variables

The API can be configured via environment variables or `appsettings.json`:

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | MySQL connection string |
| `Cognito__Region` | AWS region for Cognito |
| `Cognito__UserPoolId` | Cognito User Pool ID |
| `Cognito__AppClientId` | Cognito App Client ID |
| `Graph__TenantId` | Azure AD tenant ID |
| `Graph__ClientId` | Azure AD app client ID |
| `Graph__ClientSecret` | Azure AD app client secret |
| `Encryption__TokenEncryptionKey` | Key for encrypting OAuth tokens |

### Configuration Files

- `appsettings.json` - Base configuration (production defaults)
- `appsettings.Development.json` - Development overrides (git-ignored)
- `docker/.env` - Docker environment variables (git-ignored)

## Architecture

### Clean Architecture Layers

1. **Sorterra.Api** - Presentation layer
   - Controllers, middleware, API configuration
   - Depends on: Core, Infrastructure

2. **Sorterra.Core** - Domain layer
   - Entities, interfaces, DTOs
   - No dependencies on other projects

3. **Sorterra.Infrastructure** - Data access layer
   - DbContext, repositories, external service clients
   - Depends on: Core

### Key Design Decisions

- **Multi-tenancy via Organizations** - All data is scoped to organizations
- **JSON for flexible configs** - Recipe rules and metadata use JSON columns
- **Encrypted OAuth tokens** - SharePoint credentials are encrypted at rest
- **CHAR(36) for UUIDs** - MySQL doesn't have native UUID type
- **Comprehensive audit trail** - Activity log supports dashboard and compliance

## Development

### Building
```bash
dotnet build Sorterra.sln
```

### Running Tests
```bash
dotnet test Sorterra.sln
```

### Adding EF Core Migrations
```bash
cd src/Sorterra.Infrastructure
dotnet ef migrations add MigrationName --startup-project ../Sorterra.Api
```

### Applying Migrations
```bash
dotnet ef database update --startup-project ../Sorterra.Api
```

## Team

| Member | Role |
|--------|------|
| Zach Bagley | Backend API & Authentication |
| Patrick Petty | Frontend (React) |
| McKay Boody | Cloud Infrastructure & DevOps |
| Nate Shaw | AI/ML (Classification) |
| Caleb Gooch | AI/ML (Search & RAG) |

## License

This is a capstone project for the MISM program.
