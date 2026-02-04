# AWS ECR + Lightsail Deployment

Deploy the Sorterra API to a Lightsail instance using container images stored in ECR. This is a minimal setup for testing the agent recipe endpoint.

## Prerequisites

- AWS CLI installed and configured (`aws configure`)
- Docker running locally
- An AWS account with permissions for ECR and Lightsail

Verify your setup:

```bash
aws sts get-caller-identity
docker --version
```

## 1. Create ECR Repositories

Create one repository for each image (API and MySQL):

```bash
aws ecr create-repository --repository-name sorterra/api --region us-west-2
aws ecr create-repository --repository-name sorterra/mysql --region us-west-2
```

Note the `repositoryUri` from each output. They'll look like:

```
<ACCOUNT_ID>.dkr.ecr.us-west-2.amazonaws.com/sorterra/api
<ACCOUNT_ID>.dkr.ecr.us-west-2.amazonaws.com/sorterra/mysql
```

Set these as variables for the remaining steps:

```bash
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export AWS_REGION=us-west-2
export ECR_BASE=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com
```

## 2. Build and Push Images

Authenticate Docker with ECR:

```bash
aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin $ECR_BASE
```

Build and push from the project root (`sorterra-api/`):

```bash
# API image
docker build -t sorterra/api -f docker/api/Dockerfile .
docker tag sorterra/api:latest $ECR_BASE/sorterra/api:latest
docker push $ECR_BASE/sorterra/api:latest

# MySQL image (has schema and seed data baked in)
docker build -t sorterra/mysql -f docker/mysql/Dockerfile docker/mysql
docker tag sorterra/mysql:latest $ECR_BASE/sorterra/mysql:latest
docker push $ECR_BASE/sorterra/mysql:latest
```

## 3. Create a Lightsail Instance

Create an Ubuntu instance. The 2 GB RAM plan ($10/mo) is sufficient for MySQL + the API.

```bash
aws lightsail create-instances \
  --instance-names sorterra-test \
  --availability-zone us-west-2a \
  --blueprint-id ubuntu_24_04 \
  --bundle-id medium_3_0 \
  --region us-west-2
```

Wait for the instance to be running:

```bash
aws lightsail get-instance --instance-name sorterra-test \
  --query 'instance.state.name' --output text
```

Open port 5001 (API) in the Lightsail firewall:

```bash
aws lightsail open-instance-public-ports \
  --instance-name sorterra-test \
  --port-info fromPort=5001,toPort=5001,protocol=tcp
```

## 4. Set Up the Lightsail Instance

SSH into the instance:

```bash
ssh -i ~/.ssh/LightsailDefaultKey-us-west-2.pem ubuntu@<INSTANCE_PUBLIC_IP>
```

> You can find the public IP with:
> `aws lightsail get-instance --instance-name sorterra-test --query 'instance.publicIpAddress' --output text`
>
> Download the default SSH key from the [Lightsail console](https://lightsail.aws.amazon.com/ls/webapp/account/keys) if you don't have it, or use the browser-based SSH client in the Lightsail console.

### Install Docker and Docker Compose

```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker ubuntu
newgrp docker

# Install Docker Compose plugin
sudo apt-get install -y docker-compose-plugin

# Verify
docker --version
docker compose version
```

### Install and Configure AWS CLI

```bash
sudo apt-get install -y awscli
aws configure
# Enter your Access Key ID, Secret Access Key, region (us-west-2), and output format (json)
```

### Authenticate Docker with ECR

```bash
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export AWS_REGION=us-west-2
export ECR_BASE=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin $ECR_BASE
```

## 5. Deploy

Create the deployment files on the instance:

```bash
mkdir -p ~/sorterra && cd ~/sorterra
```

Create the `.env` file:

```bash
cat > .env << 'EOF'
ASPNETCORE_ENVIRONMENT=Development

MYSQL_ROOT_PASSWORD=changeme-root-password
MYSQL_DATABASE=sorterra_dev
MYSQL_USER=sorterra
MYSQL_PASSWORD=changeme-app-password

TOKEN_ENCRYPTION_KEY=changeme-32-byte-encryption-key!
EOF
```

> **Change the passwords above.** These are the only credentials needed for testing the recipe endpoint. Cognito and Graph API variables are not required yet.

Create the `docker-compose.yml` (replace `<ACCOUNT_ID>` with your AWS account ID):

```bash
cat > docker-compose.yml << EOF
services:
  mysql:
    image: $ECR_BASE/sorterra/mysql:latest
    container_name: sorterra-mysql
    environment:
      MYSQL_ROOT_PASSWORD: \${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: \${MYSQL_DATABASE}
      MYSQL_USER: \${MYSQL_USER}
      MYSQL_PASSWORD: \${MYSQL_PASSWORD}
    volumes:
      - mysql_data:/var/lib/mysql
    networks:
      - sorterra-network
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-p\${MYSQL_ROOT_PASSWORD}"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 60s
    restart: unless-stopped

  api:
    image: $ECR_BASE/sorterra/api:latest
    container_name: sorterra-api
    environment:
      - ASPNETCORE_ENVIRONMENT=\${ASPNETCORE_ENVIRONMENT}
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Server=mysql;Port=3306;Database=\${MYSQL_DATABASE};User=\${MYSQL_USER};Password=\${MYSQL_PASSWORD};
      - Encryption__TokenEncryptionKey=\${TOKEN_ENCRYPTION_KEY}
    ports:
      - "5001:8080"
    networks:
      - sorterra-network
    depends_on:
      mysql:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
    restart: unless-stopped

networks:
  sorterra-network:
    driver: bridge

volumes:
  mysql_data:
EOF
```

Start the services:

```bash
docker compose up -d
```

Watch the logs until both services are healthy:

```bash
docker compose logs -f
# Wait until you see "Now listening on: http://+:8080"
# Ctrl+C to exit logs
```

Check container health:

```bash
docker ps
# Both should show (healthy)
```

## 6. Load Sample Data

The schema is created automatically by the MySQL init scripts baked into the image. To add sample data for testing, run this from the Lightsail instance:

```bash
docker exec -i sorterra-mysql mysql -u sorterra -p"$(grep MYSQL_PASSWORD .env | cut -d= -f2)" sorterra_dev << 'SQL'
-- Test organization
INSERT INTO organizations (id, name, settings) VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Acme Corp', '{"plan": "professional"}');

-- Test user
INSERT INTO users (id, cognito_sub, email, display_name) VALUES
    ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'cognito-sub-sarah', 'sarah.chen@acmecorp.com', 'Sarah Chen');

-- User-org link
INSERT INTO user_organizations (user_id, organization_id, role) VALUES
    ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'owner');

-- SharePoint connection (the agent will use this ID)
INSERT INTO sharepoint_connections (id, organization_id, site_url, tenant_id, drive_id, connection_status, created_by) VALUES
    ('44444444-4444-4444-4444-444444444444', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
     'https://acmecorp.sharepoint.com/sites/Finance', 'acme-tenant-001', 'drive-finance-001',
     'active', 'cccccccc-cccc-cccc-cccc-cccccccccccc');

-- Sorting recipes (what the agent will retrieve)
INSERT INTO sorting_recipes (id, organization_id, name, description, file_type_pattern, destination_path_template, is_active, priority, created_by, rules) VALUES
    ('77777777-7777-7777-7777-777777777777', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
     'Invoice Sorting', 'Sort invoices by vendor and date',
     'Invoice', '/Finance/Invoices/[Year]/[Month]/', TRUE, 10,
     'cccccccc-cccc-cccc-cccc-cccccccccccc',
     '{"conditions": [{"field": "content_type", "operator": "equals", "value": "invoice"}], "actions": {"rename_pattern": "[Vendor]_Invoice_[Date]", "extract_fields": ["vendor", "date", "amount"]}}'),

    ('88888888-8888-8888-8888-888888888888', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
     'Contract Filing', 'Sort contracts by client and year',
     'Contract', '/Legal/Contracts/[Year]/[Client]/', TRUE, 20,
     'cccccccc-cccc-cccc-cccc-cccccccccccc',
     '{"conditions": [{"field": "content_type", "operator": "equals", "value": "contract"}], "actions": {"rename_pattern": "[Client]_Contract_[Date]", "extract_fields": ["client", "date", "value"]}}');

COMMIT;
SQL
```

## 7. Test

From your local machine (replace `<INSTANCE_IP>` with your Lightsail public IP):

```bash
# Health check
curl http://<INSTANCE_IP>:5001/health

# Get recipes by connection (the endpoint the agent uses)
curl http://<INSTANCE_IP>:5001/api/sortingrecipes/by-connection/44444444-4444-4444-4444-444444444444
```

Expected response from the recipe endpoint:

```json
[
  {
    "id": "77777777-7777-7777-7777-777777777777",
    "name": "Invoice Sorting",
    "priority": 10,
    "fileTypePattern": "Invoice",
    "destinationPathTemplate": "/Finance/Invoices/[Year]/[Month]/",
    "isActive": true,
    "rules": "{...}"
  },
  {
    "id": "88888888-8888-8888-8888-888888888888",
    "name": "Contract Filing",
    "priority": 20,
    "fileTypePattern": "Contract",
    "destinationPathTemplate": "/Legal/Contracts/[Year]/[Client]/",
    "isActive": true,
    "rules": "{...}"
  }
]
```

The agent can now call this endpoint using the Lightsail IP:

```
GET http://<INSTANCE_IP>:5001/api/sortingrecipes/by-connection/{connectionId}
```

## Updating the Deployment

When you push new code, rebuild and redeploy:

```bash
# On your local machine: build, tag, push
docker build -t sorterra/api -f docker/api/Dockerfile .
docker tag sorterra/api:latest $ECR_BASE/sorterra/api:latest
docker push $ECR_BASE/sorterra/api:latest

# On the Lightsail instance: pull and restart
docker compose pull api
docker compose up -d api
```

## Cleanup

To tear everything down:

```bash
# On the Lightsail instance
docker compose down -v

# From your local machine
aws lightsail delete-instance --instance-name sorterra-test
aws ecr delete-repository --repository-name sorterra/api --force
aws ecr delete-repository --repository-name sorterra/mysql --force
```

## Notes

- **No authentication yet.** The API is open on port 5001. This is fine for testing with the agent, but don't expose sensitive data until Cognito JWT auth is implemented.
- **ECR login expires after 12 hours.** If `docker compose pull` fails on the Lightsail instance, re-run the `aws ecr get-login-password` command from step 4.
- **MySQL data persists** in the `mysql_data` Docker volume. Use `docker compose down -v` to reset it.
- **Lightsail static IP.** By default Lightsail assigns a dynamic IP. If you need a stable IP for the agent, attach a static IP: `aws lightsail allocate-static-ip --static-ip-name sorterra-ip` then `aws lightsail attach-static-ip --static-ip-name sorterra-ip --instance-name sorterra-test`.
