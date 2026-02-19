# ============================================
# Stage 1: Build & Publish
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0.102 AS build
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

# Clean up stale build artifacts from host
RUN rm -rf src/Sorterra.Api/bin* src/Sorterra.Api/obj src/Sorterra.Core/bin* src/Sorterra.Core/obj src/Sorterra.Infrastructure/bin* src/Sorterra.Infrastructure/obj

# Publish the application
RUN dotnet publish src/Sorterra.Api/Sorterra.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ============================================
# Stage 2: Production Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Switch to the built-in non-root user
USER app

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
