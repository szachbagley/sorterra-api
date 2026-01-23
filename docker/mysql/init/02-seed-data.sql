-- Development seed data (only for local development)
-- This script runs after 01-schema.sql

USE sorterra_dev;

-- Insert test organization
INSERT INTO organizations (id, name, settings) VALUES
    ('11111111-1111-1111-1111-111111111111', 'Test Organization', '{"plan": "free"}');

-- Insert test user (you'll update cognito_sub after setting up Cognito)
INSERT INTO users (id, cognito_sub, email, display_name) VALUES
    ('22222222-2222-2222-2222-222222222222', 'test-cognito-sub', 'dev@sorterra.local', 'Dev User');

-- Link user to organization
INSERT INTO user_organizations (user_id, organization_id, role) VALUES
    ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'owner');

-- Insert sample sorting recipe
INSERT INTO sorting_recipes (id, organization_id, name, description, file_type_pattern, destination_path_template, is_active, priority, created_by, rules) VALUES
    ('33333333-3333-3333-3333-333333333333',
     '11111111-1111-1111-1111-111111111111',
     'Invoice Sorting',
     'Automatically sort invoices by vendor and date',
     'Invoice',
     '/Finance/Invoices/[Year]/[Month]/',
     TRUE,
     10,
     '22222222-2222-2222-2222-222222222222',
     '{"conditions": [{"field": "content_type", "operator": "contains", "value": "invoice"}], "actions": {"rename_pattern": "[Vendor]_Invoice_[Date]", "extract_fields": ["vendor", "date", "amount"]}}');

COMMIT;
