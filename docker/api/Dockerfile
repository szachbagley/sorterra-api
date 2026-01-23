# ============================================
# Stage 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first (better layer caching)
COPY Sorterra.sln ./
COPY src/Sorterra.Api/Sorterra.Api.csproj src/Sorterra.Api/
COPY src/Sorterra.Core/Sorterra.Core.csproj src/Sorterra.Core/
COPY src/Sorterra.Infrastructure/Sorterra.Infrastructure.csproj src/Sorterra.Infrastructure/

# Restore dependencies (cached unless .csproj files change)
RUN dotnet restore

# Copy all source code
COPY src/ src/

# Build the application
WORKDIR /src/src/Sorterra.Api
RUN dotnet build -c Release -o /app/build

# ============================================
# Stage 2: Publish
# ============================================
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ============================================
# Stage 3: Production Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' --uid 1000 appuser

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Set ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port (Kestrel default)
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Entry point
ENTRYPOINT ["dotnet", "Sorterra.Api.dll"]
