-- Sorterra Database Schema
-- This script runs automatically when the container starts for the first time

USE sorterra_dev;

-- =====================
-- CORE TABLES
-- =====================

-- Users table (synced with Cognito)
CREATE TABLE IF NOT EXISTS users (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    cognito_sub VARCHAR(255) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    display_name VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    last_login_at TIMESTAMP NULL
);

-- Organizations/Tenants (for multi-tenant support)
CREATE TABLE IF NOT EXISTS organizations (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    name VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    settings JSON DEFAULT (JSON_OBJECT())
);

-- User-Organization membership
CREATE TABLE IF NOT EXISTS user_organizations (
    user_id CHAR(36) NOT NULL,
    organization_id CHAR(36) NOT NULL,
    role VARCHAR(50) DEFAULT 'member',
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, organization_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE
);

-- =====================
-- SHAREPOINT CONNECTIONS
-- =====================

-- Connected SharePoint sites
CREATE TABLE IF NOT EXISTS sharepoint_connections (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    organization_id CHAR(36) NOT NULL,
    site_url VARCHAR(512) NOT NULL,
    tenant_id VARCHAR(255),
    client_id VARCHAR(255),
    thumbprint VARCHAR(255),
    private_key_path TEXT,
    drive_id VARCHAR(255),
    source_folder VARCHAR(1024),
    connection_status VARCHAR(50) DEFAULT 'pending',
    last_sync_at TIMESTAMP NULL,
    webhook_subscription_id VARCHAR(255),
    webhook_expiration TIMESTAMP NULL,
    created_by CHAR(36),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    error_message TEXT,

    UNIQUE KEY unique_org_site (organization_id, site_url),
    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id)
);

-- OAuth tokens (encrypted at rest)
CREATE TABLE IF NOT EXISTS oauth_tokens (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    connection_id CHAR(36) UNIQUE NOT NULL,
    access_token_encrypted BLOB NOT NULL,
    refresh_token_encrypted BLOB NOT NULL,
    token_type VARCHAR(50) DEFAULT 'Bearer',
    expires_at TIMESTAMP NOT NULL,
    scope TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    FOREIGN KEY (connection_id) REFERENCES sharepoint_connections(id) ON DELETE CASCADE
);

-- =====================
-- SORTING RECIPES
-- =====================

-- User-defined sorting rules ("Recipes")
CREATE TABLE IF NOT EXISTS sorting_recipes (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    organization_id CHAR(36) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    file_type_pattern VARCHAR(255),
    destination_path_template VARCHAR(512),
    is_active BOOLEAN DEFAULT TRUE,
    priority INT DEFAULT 0,
    created_by CHAR(36),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    rules JSON DEFAULT (JSON_OBJECT()),
    files_processed_count INT DEFAULT 0,

    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id)
);

-- =====================
-- FILE PROCESSING
-- =====================

-- Processed files log
CREATE TABLE IF NOT EXISTS processed_files (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    organization_id CHAR(36) NOT NULL,
    connection_id CHAR(36),

    sharepoint_item_id VARCHAR(255) NOT NULL,
    sharepoint_drive_id VARCHAR(255),

    original_name VARCHAR(512) NOT NULL,
    new_name VARCHAR(512),
    original_path VARCHAR(1024),
    new_path VARCHAR(1024),
    file_extension VARCHAR(50),
    file_size_bytes BIGINT,
    mime_type VARCHAR(255),

    classified_type VARCHAR(255),
    classification_confidence DECIMAL(5,4),
    applied_recipe_id CHAR(36),

    status VARCHAR(50) DEFAULT 'pending',
    processed_at TIMESTAMP NULL,
    error_message TEXT,

    extracted_metadata JSON DEFAULT (JSON_OBJECT()),

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    UNIQUE KEY unique_org_item (organization_id, sharepoint_item_id),

    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE,
    FOREIGN KEY (connection_id) REFERENCES sharepoint_connections(id) ON DELETE SET NULL,
    FOREIGN KEY (applied_recipe_id) REFERENCES sorting_recipes(id) ON DELETE SET NULL
);

-- Create indexes for common queries
CREATE INDEX idx_processed_files_org_status ON processed_files(organization_id, status);
CREATE INDEX idx_processed_files_org_date ON processed_files(organization_id, created_at DESC);

-- =====================
-- VECTOR EMBEDDINGS (RAG)
-- =====================

-- Document chunks with embeddings for semantic search
CREATE TABLE IF NOT EXISTS document_chunks (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    processed_file_id CHAR(36) NOT NULL,
    organization_id CHAR(36) NOT NULL,

    chunk_index INT NOT NULL,
    chunk_text TEXT NOT NULL,
    chunk_tokens INT,

    embedding JSON,

    page_number INT,
    section_header VARCHAR(512),

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    UNIQUE KEY unique_file_chunk (processed_file_id, chunk_index),

    FOREIGN KEY (processed_file_id) REFERENCES processed_files(id) ON DELETE CASCADE,
    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE
);

-- =====================
-- ACTIVITY & AUDIT LOG
-- =====================

-- Activity feed for dashboard
CREATE TABLE IF NOT EXISTS activity_log (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    organization_id CHAR(36) NOT NULL,
    user_id CHAR(36),

    activity_type VARCHAR(100) NOT NULL,
    entity_type VARCHAR(50),
    entity_id CHAR(36),

    description TEXT,
    metadata JSON DEFAULT (JSON_OBJECT()),

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
);

CREATE INDEX idx_activity_log_org_date ON activity_log(organization_id, created_at DESC);

-- =====================
-- WEBHOOK EVENTS (for debugging/replay)
-- =====================

CREATE TABLE IF NOT EXISTS webhook_events (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    connection_id CHAR(36) NOT NULL,

    event_type VARCHAR(100),
    resource_type VARCHAR(100),
    resource_id VARCHAR(255),

    raw_payload JSON NOT NULL,

    processing_status VARCHAR(50) DEFAULT 'received',
    processed_at TIMESTAMP NULL,
    error_message TEXT,

    received_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (connection_id) REFERENCES sharepoint_connections(id) ON DELETE CASCADE
);

CREATE INDEX idx_webhook_events_status ON webhook_events(processing_status, received_at);

-- =====================
-- SEARCH QUERIES (analytics)
-- =====================

CREATE TABLE IF NOT EXISTS search_queries (
    id CHAR(36) PRIMARY KEY DEFAULT (UUID()),
    organization_id CHAR(36) NOT NULL,
    user_id CHAR(36),

    query_text TEXT NOT NULL,
    query_embedding JSON,

    results_count INT,
    latency_ms INT,

    clicked_result_ids JSON,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
);

-- =====================
-- ADDITIONAL INDEXES
-- =====================

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_cognito ON users(cognito_sub);

COMMIT;
