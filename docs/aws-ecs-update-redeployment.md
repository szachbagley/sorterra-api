# AWS ECS Update & Redeployment

How to redeploy the Sorterra API and MySQL containers after making code or schema changes. This guide assumes the ECS infrastructure is already running (see [aws-ecs-fargate-deployment.md](aws-ecs-fargate-deployment.md) for initial setup).

## Prerequisites

- AWS CLI installed and authenticated (`aws sts get-caller-identity` should return the Sorterra account)
- Docker running locally
- ECR repositories exist (`sorterra-api`, `sorterra-mysql`)

Set environment variables used throughout this guide:

```bash
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export AWS_REGION=us-east-1
export ECR_BASE=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com
```

## 1. Authenticate Docker to ECR

```bash
aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin $ECR_BASE
```

## 2. Build and Push Images

### API Only (code changes, no schema changes)

```bash
# Build for linux/amd64 (Fargate architecture)
docker build --platform linux/amd64 -t sorterra-api -f docker/api/Dockerfile .

# Tag and push
docker tag sorterra-api:latest $ECR_BASE/sorterra-api:latest
docker push $ECR_BASE/sorterra-api:latest
```

### MySQL Only (schema or seed data changes)

```bash
docker build --platform linux/amd64 -t sorterra-mysql -f docker/mysql/Dockerfile docker/mysql

docker tag sorterra-mysql:latest $ECR_BASE/sorterra-mysql:latest
docker push $ECR_BASE/sorterra-mysql:latest
```

> **Note:** Pushing a new MySQL image updates the image for future containers, but does **not** alter the existing database. The init scripts (`01-schema.sql`, `02-seed-data.sql`) only run when MySQL initializes a fresh data directory. See [Section 4](#4-run-database-migrations) for applying schema changes to the live database.

### Both

```bash
# API
docker build --platform linux/amd64 -t sorterra-api -f docker/api/Dockerfile .
docker tag sorterra-api:latest $ECR_BASE/sorterra-api:latest
docker push $ECR_BASE/sorterra-api:latest

# MySQL
docker build --platform linux/amd64 -t sorterra-mysql -f docker/mysql/Dockerfile docker/mysql
docker tag sorterra-mysql:latest $ECR_BASE/sorterra-mysql:latest
docker push $ECR_BASE/sorterra-mysql:latest
```

## 3. Force New ECS Deployment

ECS caches the `:latest` tag. Force a new deployment to pull the updated image:

```bash
# Redeploy the API
aws ecs update-service \
  --cluster sorterra \
  --service sorterra-api-v2 \
  --force-new-deployment \
  --region $AWS_REGION
```

ECS performs a **rolling deployment** — it starts a new task with the updated image, waits for it to pass health checks, then drains and stops the old task. No downtime.

To redeploy MySQL (rarely needed — only if the MySQL image itself changed, not for schema changes):

```bash
aws ecs update-service \
  --cluster sorterra \
  --service sorterra-mysql \
  --force-new-deployment \
  --region $AWS_REGION
```

### Wait for Stability

```bash
aws ecs wait services-stable \
  --cluster sorterra \
  --services sorterra-api-v2 \
  --region $AWS_REGION

echo "API deployment complete"
```

This blocks until the new task is running and healthy (typically 1-3 minutes).

## 4. Run Database Migrations

When you've added or modified columns in `01-schema.sql`, you need to apply those changes to the live database. The init scripts only run on first creation, so the live database must be updated separately.

### Option A: One-Off Fargate Task (no extra tools required)

This approach runs a temporary Fargate task that connects to MySQL via Cloud Map DNS and executes the migration SQL, then exits.

**Step 1: Register a migration task definition**

Replace the SQL in the `command` with your migration statements:

```bash
cat > /tmp/sorterra-migrate-task.json << EOF
{
  "family": "sorterra-migrate",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "256",
  "memory": "512",
  "executionRoleArn": "arn:aws:iam::${AWS_ACCOUNT_ID}:role/sorterra-ecs-execution-role",
  "taskRoleArn": "arn:aws:iam::${AWS_ACCOUNT_ID}:role/sorterra-ecs-task-role",
  "containerDefinitions": [
    {
      "name": "migrate",
      "image": "${ECR_BASE}/sorterra-mysql:latest",
      "essential": true,
      "entryPoint": ["sh", "-c"],
      "command": [
        "mysql -h mysql.sorterra.local -u sorterra -p'changeme-app-password' sorterra_dev -e \"YOUR SQL STATEMENTS HERE;\" && mysql -h mysql.sorterra.local -u sorterra -p'changeme-app-password' sorterra_dev -e \"SHOW COLUMNS FROM your_table;\""
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/sorterra-mysql",
          "awslogs-region": "${AWS_REGION}",
          "awslogs-stream-prefix": "migrate"
        }
      }
    }
  ]
}
EOF

aws ecs register-task-definition \
  --cli-input-json file:///tmp/sorterra-migrate-task.json \
  --region $AWS_REGION
```

**Example migration SQL** (adding columns):

```sql
ALTER TABLE sharepoint_connections
  ADD COLUMN client_id VARCHAR(255) NULL,
  ADD COLUMN thumbprint VARCHAR(255) NULL;
```

> **Important:** MySQL 8.0 does not support `ADD COLUMN IF NOT EXISTS`. If a column might already exist, run separate ALTER statements and accept that one may error, or check the schema first.

**Step 2: Run the task**

The migration task must use the **API security group** (`sg-api`), not the RDS security group. The MySQL inbound rules allow connections from the API SG.

```bash
export API_SG=sg-0d557c9256b77a88b
export PRIVATE_SUBNET_1A=subnet-0c494af6d467e6dbb

MIGRATE_TASK_ARN=$(aws ecs run-task \
  --cluster sorterra \
  --task-definition sorterra-migrate \
  --launch-type FARGATE \
  --platform-version LATEST \
  --network-configuration "awsvpcConfiguration={subnets=[$PRIVATE_SUBNET_1A],securityGroups=[$API_SG],assignPublicIp=DISABLED}" \
  --query "tasks[0].taskArn" --output text \
  --region $AWS_REGION)

echo "Migration task: $MIGRATE_TASK_ARN"
```

**Step 3: Wait and verify**

```bash
aws ecs wait tasks-stopped \
  --cluster sorterra \
  --tasks $MIGRATE_TASK_ARN \
  --region $AWS_REGION

# Check exit code (0 = success)
aws ecs describe-tasks \
  --cluster sorterra \
  --tasks $MIGRATE_TASK_ARN \
  --query "tasks[0].containers[0].exitCode" \
  --output text --region $AWS_REGION
```

**Step 4: Check logs**

Extract the task ID (last segment of the ARN) to build the log stream name:

```bash
TASK_ID=$(echo $MIGRATE_TASK_ARN | rev | cut -d'/' -f1 | rev)

aws logs get-log-events \
  --log-group-name /ecs/sorterra-mysql \
  --log-stream-name "migrate/migrate/$TASK_ID" \
  --query "events[*].message" --output text \
  --region $AWS_REGION
```

**Step 5: Clean up the task definition**

```bash
# Get the revision number
REVISION=$(aws ecs describe-task-definition \
  --task-definition sorterra-migrate \
  --query "taskDefinition.revision" --output text \
  --region $AWS_REGION)

aws ecs deregister-task-definition \
  --task-definition sorterra-migrate:$REVISION \
  --region $AWS_REGION
```

### Option B: ECS Exec (requires Session Manager plugin)

If you have the [Session Manager plugin](https://docs.aws.amazon.com/systems-manager/latest/userguide/session-manager-working-with-install-plugin.html) installed, you can exec directly into the MySQL container:

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

Then run your SQL inside the container:

```bash
mysql -u sorterra -p'changeme-app-password' sorterra_dev -e "
  ALTER TABLE sharepoint_connections
    ADD COLUMN client_id VARCHAR(255) NULL,
    ADD COLUMN thumbprint VARCHAR(255) NULL;
"
```

Type `exit` when done.

> ECS Exec must be enabled on the service. If not already enabled:
> ```bash
> aws ecs update-service --cluster sorterra --service sorterra-mysql \
>   --enable-execute-command --region $AWS_REGION
> ```
> Wait for the service to redeploy before attempting to exec.

## 5. Verify the Deployment

The API is accessible via the NLB's static Elastic IPs: `35.175.101.240` and `3.230.81.125`.

Test the endpoints:

```bash
# Health check
curl -s http://35.175.101.240/health | python3 -m json.tool

# List SharePoint connections (verify new fields appear)
curl -s http://35.175.101.240/api/sharepointconnections | python3 -m json.tool

# Agent recipe endpoint
curl -s http://35.175.101.240/api/sortingrecipes/by-connection/{connectionId} | python3 -m json.tool
```

## 6. View Logs

```bash
# API logs (last 30 minutes, streaming)
aws logs tail /ecs/sorterra-api --since 30m --follow --region $AWS_REGION

# MySQL logs
aws logs tail /ecs/sorterra-mysql --since 30m --follow --region $AWS_REGION
```

## 7. Rollback

If the new deployment is unhealthy, ECS automatically keeps the old task running (the new task fails health checks and gets stopped). To manually roll back:

```bash
# Check current task status
aws ecs describe-services \
  --cluster sorterra \
  --services sorterra-api-v2 \
  --query "services[0].{desired: desiredCount, running: runningCount, pending: pendingCount, deployments: deployments[*].{status: status, running: runningCount, desired: desiredCount, rollout: rolloutState}}" \
  --output json --region $AWS_REGION
```

If a bad deployment completed, push the previous working image and force a new deployment:

```bash
# Rebuild from the last known good commit
git checkout <good-commit>
docker build --platform linux/amd64 -t sorterra-api -f docker/api/Dockerfile .
docker tag sorterra-api:latest $ECR_BASE/sorterra-api:latest
docker push $ECR_BASE/sorterra-api:latest

aws ecs update-service \
  --cluster sorterra \
  --service sorterra-api-v2 \
  --force-new-deployment \
  --region $AWS_REGION
```

For database rollback, write a reverse migration (e.g., `DROP COLUMN`) and run it using the same one-off task approach from [Section 4](#4-run-database-migrations).

## Quick Reference: Common Scenarios

| Scenario | Steps |
|----------|-------|
| Code change only (no schema) | Build API image, push, force deploy API (sections 1-3, 5) |
| Schema change only | Push MySQL image, run migration task, force deploy API (sections 1-2, 4, 3, 5) |
| Code + schema change | Push both images, run migration task, force deploy API (sections 1-4, 5) |
| Seed data change only | Push MySQL image (for future clean deploys). Insert/update data via migration task. |
| Config/env var change | Update the task definition and force deploy — no image rebuild needed |
