# AWS ECS Fargate Deployment

Deploy the Sorterra API to ECS Fargate using two containers: one for the API and one for the MySQL database. Images are stored in ECR (reused from the EC2 setup). This guide uses the existing Sorterra VPC and security groups already provisioned in us-east-1.

## Architecture

```
Internet
   │
   ▼
[ALB — sg-alb]  ←  public subnets (us-east-1a, us-east-1b)
   │
   ▼
[API Container — sg-api]  ←  private subnets, Fargate, port 8080
   │
   ▼
[MySQL Container — sg-rds]  ←  private subnet, Fargate, port 3306
   │
   ▼
[EFS Volume]  ←  Persistent MySQL data
```

Both containers run on Fargate (serverless — no EC2 instances to manage) in private subnets, reaching the internet through the existing NAT gateway. MySQL data is persisted on an EFS file system so it survives container restarts and redeployments.

## Existing Infrastructure

This guide assumes the following resources already exist in us-east-1:

| Resource | ID | Name |
|----------|----|------|
| VPC | `vpc-0d3a8af5cb4da7000` | sorterra-Dev-vpc (`10.20.0.0/16`) |
| Public subnet (1a) | `subnet-0de55b71e47a519b8` | sorterra-Dev-public-us-east-1a |
| Public subnet (1b) | `subnet-0bfbc0de171bcb5e7` | sorterra-Dev-public-us-east-1b |
| Private subnet (1a) | `subnet-0c494af6d467e6dbb` | sorterra-Dev-private-us-east-1a |
| Private subnet (1b) | `subnet-05254ca568d47c8ea` | sorterra-Dev-private-us-east-1b |
| ALB security group | `sg-095bbfe1f473c119d` | sorterra-Dev-sg-alb (HTTPS 443 in) |
| API security group | `sg-0d557c9256b77a88b` | sorterra-Dev-sg-api (8080 from ALB) |
| RDS security group | `sg-07251ea351d2a9d49` | sorterra-Dev-sg-rds (5432 from API/Jobs) |
| Internet gateway | `igw-0dd684804fa2e5b81` | sorterra-Dev-igw |
| NAT gateway | `nat-0c9800f017de238da` | sorterra-Dev-nat (in public 1a) |

## Prerequisites

- AWS CLI installed and configured (`aws configure`)
- Docker running locally
- ECR repositories already created (from the [EC2 deployment guide](aws-ec2-deployment.md))
- Images already pushed to ECR

If you haven't pushed images to ECR yet, follow steps 1–2 from the EC2 deployment guide first.

Set environment variables used throughout this guide:

```bash
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export AWS_REGION=us-east-1
export ECR_BASE=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

# Existing infrastructure
export VPC_ID=vpc-0d3a8af5cb4da7000
export PUBLIC_SUBNET_1A=subnet-0de55b71e47a519b8
export PUBLIC_SUBNET_1B=subnet-0bfbc0de171bcb5e7
export PRIVATE_SUBNET_1A=subnet-0c494af6d467e6dbb
export PRIVATE_SUBNET_1B=subnet-05254ca568d47c8ea
export ALB_SG=sg-095bbfe1f473c119d
export API_SG=sg-0d557c9256b77a88b
export RDS_SG=sg-07251ea351d2a9d49
```

## 1. Add MySQL Port to the RDS Security Group

The existing RDS security group allows Postgres on port 5432. Add an inbound rule for MySQL on port 3306 so the API container can reach the MySQL container:

```bash
aws ec2 authorize-security-group-ingress \
  --group-id $RDS_SG \
  --protocol tcp --port 3306 \
  --source-group $API_SG \
  --region $AWS_REGION
```

## 2. Create an EFS Security Group and File System

EFS provides persistent storage for MySQL data. Without it, data is lost whenever the MySQL container restarts.

Create a security group for EFS that allows NFS traffic from the RDS security group (where MySQL runs):

```bash
export EFS_SG=$(aws ec2 create-security-group \
  --group-name sorterra-Dev-sg-efs \
  --description "Sorterra EFS (Dev): NFS from MySQL container" \
  --vpc-id $VPC_ID \
  --query "GroupId" --output text --region $AWS_REGION)

aws ec2 authorize-security-group-ingress \
  --group-id $EFS_SG \
  --protocol tcp --port 2049 --source-group $RDS_SG \
  --region $AWS_REGION

# Tag it to match existing conventions
aws ec2 create-tags --resources $EFS_SG --tags \
  Key=Name,Value=sorterra-Dev-sgs \
  Key=Project,Value=Sorterra \
  Key=Environment,Value=Dev \
  --region $AWS_REGION

echo "EFS SG: $EFS_SG"
```

Create the file system:

```bash
export EFS_ID=$(aws efs create-file-system \
  --performance-mode generalPurpose \
  --throughput-mode bursting \
  --encrypted \
  --tags Key=Name,Value=sorterra-Dev-efs-mysql Key=Project,Value=Sorterra Key=Environment,Value=Dev \
  --query "FileSystemId" --output text --region $AWS_REGION)

echo "EFS ID: $EFS_ID"
```

Wait for the file system to become available:

```bash
aws efs describe-file-systems --file-system-id $EFS_ID \
  --query "FileSystems[0].LifeCycleState" --output text --region $AWS_REGION
# Should return: available
```

Create mount targets in the private subnets (where the MySQL container will run):

```bash
aws efs create-mount-target \
  --file-system-id $EFS_ID \
  --subnet-id $PRIVATE_SUBNET_1A \
  --security-groups $EFS_SG \
  --region $AWS_REGION

aws efs create-mount-target \
  --file-system-id $EFS_ID \
  --subnet-id $PRIVATE_SUBNET_1B \
  --security-groups $EFS_SG \
  --region $AWS_REGION
```

Create an access point for MySQL (sets the UID/GID to the mysql user):

```bash
export EFS_AP=$(aws efs create-access-point \
  --file-system-id $EFS_ID \
  --posix-user Uid=999,Gid=999 \
  --root-directory "Path=/mysql-data,CreationInfo={OwnerUid=999,OwnerGid=999,Permissions=755}" \
  --query "AccessPointId" --output text --region $AWS_REGION)

echo "EFS Access Point: $EFS_AP"
```

> UID/GID 999 is the default `mysql` user in the official MySQL Docker image.

## 3. Create IAM Roles

ECS tasks need two IAM roles: a **task execution role** (lets ECS pull images and write logs) and a **task role** (permissions for your application code).

### Task Execution Role

```bash
aws iam create-role \
  --role-name sorterra-ecs-execution-role \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": {"Service": "ecs-tasks.amazonaws.com"},
      "Action": "sts:AssumeRole"
    }]
  }'

aws iam attach-role-policy \
  --role-name sorterra-ecs-execution-role \
  --policy-arn arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy

export EXECUTION_ROLE_ARN=arn:aws:iam::${AWS_ACCOUNT_ID}:role/sorterra-ecs-execution-role
```

### Task Role

```bash
aws iam create-role \
  --role-name sorterra-ecs-task-role \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": {"Service": "ecs-tasks.amazonaws.com"},
      "Action": "sts:AssumeRole"
    }]
  }'

export TASK_ROLE_ARN=arn:aws:iam::${AWS_ACCOUNT_ID}:role/sorterra-ecs-task-role
```

> The task role has no policies attached yet. Add policies here later if the API needs to access other AWS services (S3, SES, etc.).

## 4. Create CloudWatch Log Groups

```bash
aws logs create-log-group --log-group-name /ecs/sorterra-api --region $AWS_REGION
aws logs create-log-group --log-group-name /ecs/sorterra-mysql --region $AWS_REGION
```

## 5. Create the ECS Cluster

```bash
aws ecs create-cluster --cluster-name sorterra --region $AWS_REGION
```

## 6. Register Task Definitions

### MySQL Task Definition

```bash
cat > /tmp/sorterra-mysql-task.json << EOF
{
  "family": "sorterra-mysql",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "executionRoleArn": "${EXECUTION_ROLE_ARN}",
  "taskRoleArn": "${TASK_ROLE_ARN}",
  "volumes": [
    {
      "name": "mysql-data",
      "efsVolumeConfiguration": {
        "fileSystemId": "${EFS_ID}",
        "transitEncryption": "ENABLED",
        "authorizationConfig": {
          "accessPointId": "${EFS_AP}",
          "iam": "DISABLED"
        }
      }
    }
  ],
  "containerDefinitions": [
    {
      "name": "mysql",
      "image": "${ECR_BASE}/sorterra-mysql:latest",
      "essential": true,
      "portMappings": [
        {
          "containerPort": 3306,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {"name": "MYSQL_ROOT_PASSWORD", "value": "changeme-root-password"},
        {"name": "MYSQL_DATABASE", "value": "sorterra_dev"},
        {"name": "MYSQL_USER", "value": "sorterra"},
        {"name": "MYSQL_PASSWORD", "value": "changeme-app-password"}
      ],
      "mountPoints": [
        {
          "sourceVolume": "mysql-data",
          "containerPath": "/var/lib/mysql"
        }
      ],
      "healthCheck": {
        "command": ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-pchangeme-root-password"],
        "interval": 30,
        "timeout": 10,
        "retries": 5,
        "startPeriod": 120
      },
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/sorterra-mysql",
          "awslogs-region": "${AWS_REGION}",
          "awslogs-stream-prefix": "mysql"
        }
      }
    }
  ]
}
EOF

aws ecs register-task-definition \
  --cli-input-json file:///tmp/sorterra-mysql-task.json \
  --region $AWS_REGION
```

> **Change the passwords above.** For production, use AWS Secrets Manager instead of plain-text environment variables (see the [Using Secrets Manager](#using-secrets-manager-optional) section at the bottom).

### API Task Definition

```bash
cat > /tmp/sorterra-api-task.json << EOF
{
  "family": "sorterra-api",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "256",
  "memory": "512",
  "executionRoleArn": "${EXECUTION_ROLE_ARN}",
  "taskRoleArn": "${TASK_ROLE_ARN}",
  "containerDefinitions": [
    {
      "name": "api",
      "image": "${ECR_BASE}/sorterra-api:latest",
      "essential": true,
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {"name": "ASPNETCORE_ENVIRONMENT", "value": "Development"},
        {"name": "ASPNETCORE_URLS", "value": "http://+:8080"},
        {"name": "ConnectionStrings__DefaultConnection", "value": "Server=mysql.sorterra.local;Port=3306;Database=sorterra_dev;User=sorterra;Password=changeme-app-password;"},
        {"name": "Encryption__TokenEncryptionKey", "value": "changeme-32-byte-encryption-key!"}
      ],
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost:8080/health/live || exit 1"],
        "interval": 30,
        "timeout": 10,
        "retries": 3,
        "startPeriod": 15
      },
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/sorterra-api",
          "awslogs-region": "${AWS_REGION}",
          "awslogs-stream-prefix": "api"
        }
      }
    }
  ]
}
EOF

aws ecs register-task-definition \
  --cli-input-json file:///tmp/sorterra-api-task.json \
  --region $AWS_REGION
```

## 7. Create Service Discovery

Service discovery lets the API container find the MySQL container by DNS name (`mysql.sorterra.local`) instead of hardcoded IPs.

```bash
aws servicediscovery create-private-dns-namespace \
  --name sorterra.local \
  --vpc $VPC_ID \
  --region $AWS_REGION
```

Wait for the namespace to be created, then get its ID:

```bash
sleep 10
export NAMESPACE_ID=$(aws servicediscovery list-namespaces \
  --query "Namespaces[?Name=='sorterra.local'].Id" \
  --output text --region $AWS_REGION)

echo "Namespace: $NAMESPACE_ID"
```

Create a service discovery service for MySQL:

```bash
export MYSQL_DISCOVERY_ARN=$(aws servicediscovery create-service \
  --name mysql \
  --dns-config "NamespaceId=$NAMESPACE_ID,DnsRecords=[{Type=A,TTL=10}]" \
  --health-check-custom-config FailureThreshold=1 \
  --query "Service.Arn" --output text --region $AWS_REGION)

export MYSQL_DISCOVERY_ID=$(echo $MYSQL_DISCOVERY_ARN | rev | cut -d'/' -f1 | rev)

echo "MySQL Discovery Service: $MYSQL_DISCOVERY_ID"
```

## 8. Deploy the MySQL Service

MySQL runs in a private subnet. The NAT gateway provides outbound internet access for pulling images from ECR.

```bash
aws ecs create-service \
  --cluster sorterra \
  --service-name sorterra-mysql \
  --task-definition sorterra-mysql \
  --desired-count 1 \
  --launch-type FARGATE \
  --platform-version LATEST \
  --network-configuration "awsvpcConfiguration={subnets=[$PRIVATE_SUBNET_1A],securityGroups=[$RDS_SG],assignPublicIp=DISABLED}" \
  --service-registries "registryArn=$MYSQL_DISCOVERY_ARN" \
  --region $AWS_REGION
```

> `assignPublicIp=DISABLED` because private subnets reach ECR through the NAT gateway. No public IP needed.

Wait for MySQL to be healthy before moving on:

```bash
aws ecs wait services-stable \
  --cluster sorterra \
  --services sorterra-mysql \
  --region $AWS_REGION

echo "MySQL service is stable"
```

This can take 2–3 minutes. You can also check the status in the [ECS console](https://console.aws.amazon.com/ecs/).

## 9. Create the Application Load Balancer

The ALB sits in the public subnets and uses the existing ALB security group. The existing SG allows HTTPS (443) inbound. We'll also add an HTTP (80) listener for testing.

Add an HTTP inbound rule to the ALB security group:

```bash
aws ec2 authorize-security-group-ingress \
  --group-id $ALB_SG \
  --protocol tcp --port 80 --cidr 0.0.0.0/0 \
  --region $AWS_REGION
```

Create the ALB in the public subnets:

```bash
export ALB_ARN=$(aws elbv2 create-load-balancer \
  --name sorterra-alb \
  --subnets $PUBLIC_SUBNET_1A $PUBLIC_SUBNET_1B \
  --security-groups $ALB_SG \
  --scheme internet-facing \
  --type application \
  --query "LoadBalancers[0].LoadBalancerArn" --output text --region $AWS_REGION)

export ALB_DNS=$(aws elbv2 describe-load-balancers \
  --load-balancer-arns $ALB_ARN \
  --query "LoadBalancers[0].DNSName" --output text --region $AWS_REGION)

echo "ALB DNS: $ALB_DNS"
```

Create a target group for the API:

```bash
export TG_ARN=$(aws elbv2 create-target-group \
  --name sorterra-api-tg \
  --protocol HTTP \
  --port 8080 \
  --vpc-id $VPC_ID \
  --target-type ip \
  --health-check-path /health/live \
  --health-check-interval-seconds 30 \
  --healthy-threshold-count 2 \
  --unhealthy-threshold-count 3 \
  --query "TargetGroups[0].TargetGroupArn" --output text --region $AWS_REGION)

echo "Target Group: $TG_ARN"
```

Create a listener that forwards port 80 to the target group:

```bash
aws elbv2 create-listener \
  --load-balancer-arn $ALB_ARN \
  --protocol HTTP \
  --port 80 \
  --default-actions Type=forward,TargetGroupArn=$TG_ARN \
  --region $AWS_REGION
```

## 10. Deploy the API Service

The API runs in private subnets behind the ALB:

```bash
aws ecs create-service \
  --cluster sorterra \
  --service-name sorterra-api \
  --task-definition sorterra-api \
  --desired-count 1 \
  --launch-type FARGATE \
  --platform-version LATEST \
  --network-configuration "awsvpcConfiguration={subnets=[$PRIVATE_SUBNET_1A,$PRIVATE_SUBNET_1B],securityGroups=[$API_SG],assignPublicIp=DISABLED}" \
  --load-balancers "targetGroupArn=$TG_ARN,containerName=api,containerPort=8080" \
  --region $AWS_REGION
```

Wait for the API to stabilize:

```bash
aws ecs wait services-stable \
  --cluster sorterra \
  --services sorterra-api \
  --region $AWS_REGION

echo "API service is stable"
```

## 11. Load Sample Data

Use ECS Exec to run commands inside the MySQL container.

First, enable ECS Exec on the MySQL service:

```bash
aws ecs update-service \
  --cluster sorterra \
  --service sorterra-mysql \
  --enable-execute-command \
  --region $AWS_REGION
```

Attach the SSM policy to the task role so ECS Exec works:

```bash
aws iam attach-role-policy \
  --role-name sorterra-ecs-task-role \
  --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore
```

Wait for the service to redeploy with exec enabled (this replaces the task):

```bash
aws ecs wait services-stable \
  --cluster sorterra \
  --services sorterra-mysql \
  --region $AWS_REGION
```

Get the MySQL task ID and exec into it:

```bash
MYSQL_TASK=$(aws ecs list-tasks \
  --cluster sorterra \
  --service-name sorterra-mysql \
  --query "taskArns[0]" --output text --region $AWS_REGION)

aws ecs execute-command \
  --cluster sorterra \
  --task $MYSQL_TASK \
  --container mysql \
  --interactive \
  --command "/bin/bash" \
  --region $AWS_REGION
```

> You need the [Session Manager plugin](https://docs.aws.amazon.com/systems-manager/latest/userguide/session-manager-working-with-install-plugin.html) installed locally for `execute-command` to work.

Once inside the container, load sample data:

```bash
mysql -u sorterra -p"changeme-app-password" sorterra_dev << 'SQL'
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

Type `exit` to leave the container.

## 12. Test

The API is reachable through the ALB DNS name:

```bash
# Health check
curl http://$ALB_DNS/health

# Get recipes by connection (the endpoint the agent uses)
curl http://$ALB_DNS/api/sortingrecipes/by-connection/44444444-4444-4444-4444-444444444444
```

Expected responses are the same as in the [EC2 deployment guide](aws-ec2-deployment.md#7-test).

The agent can now call this endpoint using the ALB DNS name:

```
GET http://<ALB_DNS>/api/sortingrecipes/by-connection/{connectionId}
```

> The ALB DNS name is stable — unlike EC2 public IPs, it doesn't change on restarts.

## Updating the Deployment

When you push new code, rebuild and redeploy:

```bash
# On your local machine: build, tag, push
docker build --platform linux/amd64 -t sorterra-api -f docker/api/Dockerfile .
docker tag sorterra-api:latest $ECR_BASE/sorterra-api:latest
docker push $ECR_BASE/sorterra-api:latest

# Force ECS to pull the new image and restart
aws ecs update-service \
  --cluster sorterra \
  --service sorterra-api \
  --force-new-deployment \
  --region $AWS_REGION
```

ECS performs a rolling deployment — it starts a new task with the updated image, waits for it to pass health checks, then drains and stops the old task. No downtime.

To update the MySQL image:

```bash
docker build --platform linux/amd64 -t sorterra-mysql -f docker/mysql/Dockerfile docker/mysql
docker tag sorterra-mysql:latest $ECR_BASE/sorterra-mysql:latest
docker push $ECR_BASE/sorterra-mysql:latest

aws ecs update-service \
  --cluster sorterra \
  --service sorterra-mysql \
  --force-new-deployment \
  --region $AWS_REGION
```

## Viewing Logs

Logs go to CloudWatch. View them from the CLI:

```bash
# API logs (last 30 minutes)
aws logs tail /ecs/sorterra-api --since 30m --follow --region $AWS_REGION

# MySQL logs
aws logs tail /ecs/sorterra-mysql --since 30m --follow --region $AWS_REGION
```

Or view them in the [CloudWatch console](https://console.aws.amazon.com/cloudwatch/) under **Log groups**.

## Cleanup

Delete the resources created by this guide. The existing VPC, subnets, and security groups are left untouched.

```bash
# 1. Delete ECS services
aws ecs update-service --cluster sorterra --service sorterra-api --desired-count 0 --region $AWS_REGION
aws ecs update-service --cluster sorterra --service sorterra-mysql --desired-count 0 --region $AWS_REGION
aws ecs delete-service --cluster sorterra --service sorterra-api --force --region $AWS_REGION
aws ecs delete-service --cluster sorterra --service sorterra-mysql --force --region $AWS_REGION

# 2. Delete the ALB, listener, and target group
LISTENER_ARN=$(aws elbv2 describe-listeners --load-balancer-arn $ALB_ARN \
  --query "Listeners[0].ListenerArn" --output text --region $AWS_REGION)
aws elbv2 delete-listener --listener-arn $LISTENER_ARN --region $AWS_REGION
aws elbv2 delete-target-group --target-group-arn $TG_ARN --region $AWS_REGION
aws elbv2 delete-load-balancer --load-balancer-arn $ALB_ARN --region $AWS_REGION

# 3. Delete ECS cluster (wait a minute for services to drain)
sleep 60
aws ecs delete-cluster --cluster sorterra --region $AWS_REGION

# 4. Delete service discovery
aws servicediscovery delete-service --id $MYSQL_DISCOVERY_ID --region $AWS_REGION
aws servicediscovery delete-namespace --id $NAMESPACE_ID --region $AWS_REGION

# 5. Deregister task definitions
aws ecs deregister-task-definition --task-definition sorterra-api:1 --region $AWS_REGION
aws ecs deregister-task-definition --task-definition sorterra-mysql:1 --region $AWS_REGION

# 6. Delete EFS (remove mount targets first)
for MT in $(aws efs describe-mount-targets --file-system-id $EFS_ID \
  --query "MountTargets[*].MountTargetId" --output text --region $AWS_REGION); do
  aws efs delete-mount-target --mount-target-id $MT --region $AWS_REGION
done
sleep 30
aws efs delete-file-system --file-system-id $EFS_ID --region $AWS_REGION

# 7. Delete EFS security group
aws ec2 delete-security-group --group-id $EFS_SG --region $AWS_REGION

# 8. Remove the MySQL port rule from the RDS security group
aws ec2 revoke-security-group-ingress \
  --group-id $RDS_SG \
  --protocol tcp --port 3306 --source-group $API_SG \
  --region $AWS_REGION

# 9. Remove the HTTP rule from the ALB security group
aws ec2 revoke-security-group-ingress \
  --group-id $ALB_SG \
  --protocol tcp --port 80 --cidr 0.0.0.0/0 \
  --region $AWS_REGION

# 10. Delete CloudWatch log groups
aws logs delete-log-group --log-group-name /ecs/sorterra-api --region $AWS_REGION
aws logs delete-log-group --log-group-name /ecs/sorterra-mysql --region $AWS_REGION

# 11. Delete IAM roles
aws iam detach-role-policy --role-name sorterra-ecs-execution-role \
  --policy-arn arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy
aws iam delete-role --role-name sorterra-ecs-execution-role

aws iam detach-role-policy --role-name sorterra-ecs-task-role \
  --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore
aws iam delete-role --role-name sorterra-ecs-task-role
```

Optionally delete the ECR repositories (shared with the EC2 deployment):

```bash
aws ecr delete-repository --repository-name sorterra-api --force --region $AWS_REGION
aws ecr delete-repository --repository-name sorterra-mysql --force --region $AWS_REGION
```

## Notes

- **No authentication yet.** The API is open on the ALB. This is fine for testing with the agent, but add Cognito JWT auth before exposing sensitive data.
- **MySQL on Fargate is for testing.** For production, consider Amazon RDS for MySQL — it handles backups, failover, and patching automatically. The API connection string is the only thing that changes. The RDS security group is already set up for this.
- **EFS performance.** EFS with bursting throughput is fine for light workloads. If MySQL performance is slow, switch to provisioned throughput or migrate to RDS.
- **Cost.** Fargate pricing is based on vCPU and memory per second. The setup in this guide (0.25 vCPU + 0.5 GB for API, 0.5 vCPU + 1 GB for MySQL) costs roughly **~$25–30/month** running 24/7. Stop the services when not in use by setting desired count to 0:
  ```bash
  aws ecs update-service --cluster sorterra --service sorterra-api --desired-count 0 --region $AWS_REGION
  aws ecs update-service --cluster sorterra --service sorterra-mysql --desired-count 0 --region $AWS_REGION
  ```
  Restart by setting desired count back to 1.
- **NAT gateway cost.** The existing NAT gateway costs ~$32/month plus data transfer. This is needed for private subnet containers to pull ECR images and reach the internet.
- **Scaling.** To run multiple API containers behind the ALB, change `--desired-count` to 2 or more. The ALB distributes traffic automatically. Don't scale MySQL this way — use RDS with read replicas instead.
- **HTTPS.** The ALB security group already allows HTTPS (443). To use it, request a certificate from ACM, add an HTTPS listener on port 443 to the ALB, and remove the HTTP listener (or redirect 80 to 443).

## Using Secrets Manager (optional)

For production, store sensitive values in AWS Secrets Manager instead of plain-text environment variables.

Create a secret:

```bash
aws secretsmanager create-secret \
  --name sorterra/db-credentials \
  --secret-string '{"MYSQL_ROOT_PASSWORD":"your-root-password","MYSQL_PASSWORD":"your-app-password","TOKEN_ENCRYPTION_KEY":"your-32-byte-key"}' \
  --region $AWS_REGION
```

Add a policy to the execution role so ECS can read the secret:

```bash
aws iam put-role-policy \
  --role-name sorterra-ecs-execution-role \
  --policy-name SecretsManagerAccess \
  --policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Action": ["secretsmanager:GetSecretValue"],
      "Resource": "arn:aws:secretsmanager:'$AWS_REGION':'$AWS_ACCOUNT_ID':secret:sorterra/*"
    }]
  }'
```

Then in the task definition, replace `environment` entries with `secrets`:

```json
"secrets": [
  {
    "name": "MYSQL_ROOT_PASSWORD",
    "valueFrom": "arn:aws:secretsmanager:us-east-1:<ACCOUNT_ID>:secret:sorterra/db-credentials:MYSQL_ROOT_PASSWORD::"
  },
  {
    "name": "MYSQL_PASSWORD",
    "valueFrom": "arn:aws:secretsmanager:us-east-1:<ACCOUNT_ID>:secret:sorterra/db-credentials:MYSQL_PASSWORD::"
  }
]
```
