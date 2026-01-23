using Microsoft.EntityFrameworkCore;
using Sorterra.Core.Entities;

namespace Sorterra.Infrastructure.Data;

public class SorterraDbContext : DbContext
{
    public SorterraDbContext(DbContextOptions<SorterraDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();
    public DbSet<SharePointConnection> SharePointConnections => Set<SharePointConnection>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<SortingRecipe> SortingRecipes => Set<SortingRecipe>();
    public DbSet<ProcessedFile> ProcessedFiles => Set<ProcessedFile>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<SearchQuery> SearchQueries => Set<SearchQuery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.CognitoSub).HasColumnName("cognito_sub").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.HasIndex(e => e.CognitoSub).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Organization
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Settings).HasColumnName("settings").HasColumnType("json");
        });

        // UserOrganization (junction table)
        modelBuilder.Entity<UserOrganization>(entity =>
        {
            entity.ToTable("user_organizations");
            entity.HasKey(e => new { e.UserId, e.OrganizationId });
            entity.Property(e => e.UserId).HasColumnName("user_id").HasColumnType("char(36)");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id").HasColumnType("char(36)");
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(50);
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at");

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserOrganizations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.UserOrganizations)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SharePointConnection
        modelBuilder.Entity<SharePointConnection>(entity =>
        {
            entity.ToTable("sharepoint_connections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id").HasColumnType("char(36)");
            entity.Property(e => e.SiteUrl).HasColumnName("site_url").HasMaxLength(512).IsRequired();
            entity.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(255);
            entity.Property(e => e.DriveId).HasColumnName("drive_id").HasMaxLength(255);
            entity.Property(e => e.ConnectionStatus).HasColumnName("connection_status").HasMaxLength(50);
            entity.Property(e => e.LastSyncAt).HasColumnName("last_sync_at");
            entity.Property(e => e.WebhookSubscriptionId).HasColumnName("webhook_subscription_id").HasMaxLength(255);
            entity.Property(e => e.WebhookExpiration).HasColumnName("webhook_expiration");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("char(36)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasColumnType("text");

            entity.HasIndex(e => new { e.OrganizationId, e.SiteUrl }).IsUnique();

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.SharePointConnections)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // OAuthToken
        modelBuilder.Entity<OAuthToken>(entity =>
        {
            entity.ToTable("oauth_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.ConnectionId).HasColumnName("connection_id").HasColumnType("char(36)");
            entity.Property(e => e.AccessTokenEncrypted).HasColumnName("access_token_encrypted").HasColumnType("blob").IsRequired();
            entity.Property(e => e.RefreshTokenEncrypted).HasColumnName("refresh_token_encrypted").HasColumnType("blob").IsRequired();
            entity.Property(e => e.TokenType).HasColumnName("token_type").HasMaxLength(50);
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Scope).HasColumnName("scope").HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.ConnectionId).IsUnique();

            entity.HasOne(e => e.Connection)
                .WithOne(c => c.OAuthToken)
                .HasForeignKey<OAuthToken>(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SortingRecipe
        modelBuilder.Entity<SortingRecipe>(entity =>
        {
            entity.ToTable("sorting_recipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id").HasColumnType("char(36)");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasColumnType("text");
            entity.Property(e => e.FileTypePattern).HasColumnName("file_type_pattern").HasMaxLength(255);
            entity.Property(e => e.DestinationPathTemplate).HasColumnName("destination_path_template").HasMaxLength(512);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Priority).HasColumnName("priority");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("char(36)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Rules).HasColumnName("rules").HasColumnType("json");
            entity.Property(e => e.FilesProcessedCount).HasColumnName("files_processed_count");

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.SortingRecipes)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ProcessedFile
        modelBuilder.Entity<ProcessedFile>(entity =>
        {
            entity.ToTable("processed_files");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id").HasColumnType("char(36)");
            entity.Property(e => e.ConnectionId).HasColumnName("connection_id").HasColumnType("char(36)");
            entity.Property(e => e.SharePointItemId).HasColumnName("sharepoint_item_id").HasMaxLength(255).IsRequired();
            entity.Property(e => e.SharePointDriveId).HasColumnName("sharepoint_drive_id").HasMaxLength(255);
            entity.Property(e => e.OriginalName).HasColumnName("original_name").HasMaxLength(512).IsRequired();
            entity.Property(e => e.NewName).HasColumnName("new_name").HasMaxLength(512);
            entity.Property(e => e.OriginalPath).HasColumnName("original_path").HasMaxLength(1024);
            entity.Property(e => e.NewPath).HasColumnName("new_path").HasMaxLength(1024);
            entity.Property(e => e.FileExtension).HasColumnName("file_extension").HasMaxLength(50);
            entity.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(e => e.MimeType).HasColumnName("mime_type").HasMaxLength(255);
            entity.Property(e => e.ClassifiedType).HasColumnName("classified_type").HasMaxLength(255);
            entity.Property(e => e.ClassificationConfidence).HasColumnName("classification_confidence").HasColumnType("decimal(5,4)");
            entity.Property(e => e.AppliedRecipeId).HasColumnName("applied_recipe_id").HasColumnType("char(36)");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
            entity.Property(e => e.ExtractedMetadata).HasColumnName("extracted_metadata").HasColumnType("json");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => new { e.OrganizationId, e.SharePointItemId }).IsUnique();
            entity.HasIndex(e => new { e.OrganizationId, e.Status });
            entity.HasIndex(e => new { e.OrganizationId, e.CreatedAt });

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.ProcessedFiles)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Connection)
                .WithMany()
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.AppliedRecipe)
                .WithMany()
                .HasForeignKey(e => e.AppliedRecipeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // DocumentChunk
        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.ProcessedFileId).HasColumnName("processed_file_id").HasColumnType("char(36)");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id").HasColumnType("char(36)");
            entity.Property(e => e.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(e => e.ChunkText).HasColumnName("chunk_text").HasColumnType("text").IsRequired();
            entity.Property(e => e.ChunkTokens).HasColumnName("chunk_tokens");
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("json");
            entity.Property(e => e.PageNumber).HasColumnName("page_number");
            entity.Property(e => e.SectionHeader).HasColumnName("section_header").HasMaxLength(512);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => new { e.ProcessedFileId, e.ChunkIndex }).IsUnique();

            entity.HasOne(e => e.ProcessedFile)
                .WithMany(f => f.DocumentChunks)
                .HasForeignKey(e => e.ProcessedFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ActivityLog
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("activity_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id").HasColumnType("char(36)");
            entity.Property(e => e.UserId).HasColumnName("user_id").HasColumnType("char(36)");
            entity.Property(e => e.ActivityType).HasColumnName("activity_type").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(50);
            entity.Property(e => e.EntityId).HasColumnName("entity_id").HasColumnType("char(36)");
            entity.Property(e => e.Description).HasColumnName("description").HasColumnType("text");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("json");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => new { e.OrganizationId, e.CreatedAt });

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.ActivityLogs)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // WebhookEvent
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("webhook_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.ConnectionId).HasColumnName("connection_id").HasColumnType("char(36)");
            entity.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100);
            entity.Property(e => e.ResourceType).HasColumnName("resource_type").HasMaxLength(100);
            entity.Property(e => e.ResourceId).HasColumnName("resource_id").HasMaxLength(255);
            entity.Property(e => e.RawPayload).HasColumnName("raw_payload").HasColumnType("json").IsRequired();
            entity.Property(e => e.ProcessingStatus).HasColumnName("processing_status").HasMaxLength(50);
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
            entity.Property(e => e.ReceivedAt).HasColumnName("received_at");

            entity.HasIndex(e => new { e.ProcessingStatus, e.ReceivedAt });

            entity.HasOne(e => e.Connection)
                .WithMany()
                .HasForeignKey(e => e.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SearchQuery
        modelBuilder.Entity<SearchQuery>(entity =>
        {
            entity.ToTable("search_queries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id").HasColumnType("char(36)");
            entity.Property(e => e.UserId).HasColumnName("user_id").HasColumnType("char(36)");
            entity.Property(e => e.QueryText).HasColumnName("query_text").HasColumnType("text").IsRequired();
            entity.Property(e => e.QueryEmbedding).HasColumnName("query_embedding").HasColumnType("json");
            entity.Property(e => e.ResultsCount).HasColumnName("results_count");
            entity.Property(e => e.LatencyMs).HasColumnName("latency_ms");
            entity.Property(e => e.ClickedResultIds).HasColumnName("clicked_result_ids").HasColumnType("json");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
