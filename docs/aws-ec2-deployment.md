# AWS ECR + EC2 Deployment

Deploy the Sorterra API to an EC2 instance using container images stored in ECR. This is a minimal setup for testing the agent recipe endpoint.

## Prerequisites

- AWS CLI installed and configured (`aws configure`)
- Docker running locally
- An AWS account with permissions for ECR and EC2

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
docker build --platform linux/amd64 -t sorterra/api -f docker/api/Dockerfile .
docker tag sorterra/api:latest $ECR_BASE/sorterra/api:latest
docker push $ECR_BASE/sorterra/api:latest

# MySQL image (has schema and seed data baked in)
docker build --platform linux/amd64 -t sorterra/mysql -f docker/mysql/Dockerfile docker/mysql
docker tag sorterra/mysql:latest $ECR_BASE/sorterra/mysql:latest
docker push $ECR_BASE/sorterra/mysql:latest
```

## 3. Create an EC2 Instance

Create an Ubuntu instance. A `t2.micro` is free-tier eligible (1 GB RAM). This is tight for MySQL + the API, so we'll add a swap file in step 4.

> If you're not on the free tier or need more headroom, use a `t3.small` (2 GB RAM, ~$15/mo) instead.

### Create a key pair

1. Go to the [EC2 console](https://console.aws.amazon.com/ec2/) and make sure your region is set to **US West (Oregon) us-west-2** in the top-right dropdown.
2. In the left sidebar, go to **Network & Security** → **Key Pairs**.
3. Click **Create key pair**.
4. Enter **sorterra-test** as the name, select **RSA** and **.pem** format.
5. Click **Create key pair** — your browser will download `sorterra-test.pem`.
6. Move the key and set permissions:

```bash
mv ~/Downloads/sorterra-test.pem ~/.ssh/sorterra-test.pem
chmod 400 ~/.ssh/sorterra-test.pem
```

### Launch the instance

1. In the left sidebar, go to **Instances** → **Instances**, then click **Launch instances**.
2. Under **Name**, enter **sorterra-test**.
3. Under **Application and OS Images**, select **Ubuntu** and pick **Ubuntu Server 24.04 LTS (Free tier eligible)**.
4. Under **Instance type**, select **t2.micro** (Free tier eligible).
5. Under **Key pair**, select the **sorterra-test** key pair you just created.
6. Under **Network settings**, click **Edit** and configure the security group:
   - **Security group name**: `sorterra-test-sg`
   - **Rule 1** (pre-filled): Type **SSH**, Port **22**, Source **My IP**
   - Click **Add security group rule**: Type **Custom TCP**, Port **5001**, Source **Anywhere** (0.0.0.0/0)
7. Under **Configure storage**, keep the default (8 GB gp3 is fine).
8. Click **Launch instance**.

Wait for the instance state to show **Running** on the Instances page. Note the **Public IPv4 address** — you'll need it for SSH and testing.

## 4. Set Up the EC2 Instance

SSH into the instance:

```bash
ssh -i ~/.ssh/sorterra-test.pem ubuntu@<INSTANCE_PUBLIC_IP>
```

> You can find the public IP on the **Instances** page of the [EC2 console](https://console.aws.amazon.com/ec2/), or you can use the **EC2 Instance Connect** button on the instance details page to open a browser-based terminal.

### Add swap space (recommended for t2.micro)

```bash
sudo fallocate -l 1G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

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

The schema is created automatically by the MySQL init scripts baked into the image. To add sample data for testing, run this from the EC2 instance:

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

From your local machine (replace `<INSTANCE_IP>` with your EC2 public IP):

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

The agent can now call this endpoint using the EC2 public IP:

```
GET http://<INSTANCE_IP>:5001/api/sortingrecipes/by-connection/{connectionId}
```

## Updating the Deployment

When you push new code, rebuild and redeploy:

```bash
# On your local machine: build, tag, push
docker build --platform linux/amd64 -t sorterra/api -f docker/api/Dockerfile .
docker tag sorterra/api:latest $ECR_BASE/sorterra/api:latest
docker push $ECR_BASE/sorterra/api:latest

# On the EC2 instance: pull and restart
docker compose pull api
docker compose up -d api
```

## Cleanup

To tear everything down:

```bash
# On the EC2 instance
docker compose down -v
```

Then from the [EC2 console](https://console.aws.amazon.com/ec2/):

1. Go to **Instances**, select **sorterra-test**, then **Instance state** → **Terminate instance**.
2. Go to **Network & Security** → **Security Groups**, select **sorterra-test-sg**, then **Actions** → **Delete security groups**.
3. Go to **Network & Security** → **Key Pairs**, select **sorterra-test**, then **Actions** → **Delete**.

Delete the ECR repositories:

```bash
aws ecr delete-repository --repository-name sorterra/api --force
aws ecr delete-repository --repository-name sorterra/mysql --force
```

## Notes

- **No authentication yet.** The API is open on port 5001. This is fine for testing with the agent, but don't expose sensitive data until Cognito JWT auth is implemented.
- **ECR login expires after 12 hours.** If `docker compose pull` fails on the EC2 instance, re-run the `aws ecr get-login-password` command from step 4.
- **MySQL data persists** in the `mysql_data` Docker volume. Use `docker compose down -v` to reset it.
- **EC2 public IP changes on reboot.** If you stop and start the instance, it gets a new public IP. To keep a stable IP, allocate an Elastic IP from the EC2 console (**Network & Security** → **Elastic IPs** → **Allocate Elastic IP address**), then associate it with your instance.
- **Free tier limits.** The `t2.micro` free tier includes 750 hours/month for 12 months. Running one instance 24/7 uses ~720 hours, so you're covered. Stop the instance when not in use to stay well within limits.
