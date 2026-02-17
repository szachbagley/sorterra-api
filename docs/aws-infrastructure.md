# AWS Infrastructure

This document describes the AWS infrastructure that runs the Sorterra API and database in us-east-1 (N. Virginia).

## Overview Diagram

```
                            ┌─────────────────────────────────────────────────────────────────────┐
                            │  AWS Account 896170900648 · us-east-1                               │
                            │                                                                     │
                            │  ┌───────────────────────────────────────────────────────────────┐  │
                            │  │  VPC: sorterra-Dev-vpc (vpc-0d3a8af5cb4da7000)                │  │
                            │  │  CIDR: 10.20.0.0/16                                           │  │
                            │  │                                                               │  │
┌──────────┐   HTTP/443     │  │  ┌─────────────────────────────────────────────────────────┐  │  │
│          │   HTTP/80      │  │  │  Public Subnets                                         │  │  │
│ Internet │ ──────────────►│  │  │                                                         │  │  │
│          │                │  │  │  ┌─────────────────────┐  ┌─────────────────────┐       │  │  │
└──────────┘                │  │  │  │ us-east-1a          │  │ us-east-1b          │       │  │  │
     ▲                      │  │  │  │ 10.20.0.0/24        │  │ 10.20.1.0/24        │       │  │  │
     │                      │  │  │  │                     │  │                     │       │  │  │
     │                      │  │  │  │  ┌──────────────────┴──┴──────────────────┐  │       │  │  │
     │                      │  │  │  │  │  ALB: sorterra-alb                     │  │       │  │  │
     │                      │  │  │  │  │  sg-alb: 80, 443 from 0.0.0.0/0       │  │       │  │  │
     │                      │  │  │  │  └──────────────┬─────────────────────────┘  │       │  │  │
     │                      │  │  │  │                 │ :8080                      │       │  │  │
     │                      │  │  │  │  ┌──────────┐   │                            │       │  │  │
     │                      │  │  │  │  │ NAT GW   │   │                            │       │  │  │
     │                      │  │  │  │  │ 98.91.   │   │                            │       │  │  │
     │                      │  │  │  │  │ 120.32   │   │                            │       │  │  │
     │                      │  │  │  └──┴──────────┴───┼────────────────────────────┘       │  │  │
     │                      │  │  └────────────────────┼────────────────────────────────────┘  │  │
     │                      │  │                       │                                       │  │
     │                      │  │  ┌────────────────────┼────────────────────────────────────┐  │  │
     │                      │  │  │  Private Subnets   │                                    │  │  │
     │                      │  │  │                    ▼                                    │  │  │
     │                      │  │  │  ┌─────────────────────┐  ┌─────────────────────┐      │  │  │
     │                      │  │  │  │ us-east-1a          │  │ us-east-1b          │      │  │  │
     │                      │  │  │  │ 10.20.10.0/24       │  │ 10.20.11.0/24       │      │  │  │
     │                      │  │  │  │                     │  │                     │      │  │  │
     │                      │  │  │  │  ┌──────────────┐   │  │                     │      │  │  │
     │                      │  │  │  │  │  ECS Fargate │   │  │                     │      │  │  │
     │                      │  │  │  │  │  API Service │   │  │                     │      │  │  │
     │                      │  │  │  │  │  :8080       │   │  │                     │      │  │  │
     │                      │  │  │  │  │  sg-api      │   │  │                     │      │  │  │
     │                      │  │  │  │  └──────┬───────┘   │  │                     │      │  │  │
     │                      │  │  │  │         │ :3306     │  │                     │      │  │  │
     │                      │  │  │  │         ▼           │  │                     │      │  │  │
     │                      │  │  │  │  ┌──────────────┐   │  │                     │      │  │  │
     │                      │  │  │  │  │  ECS Fargate │   │  │                     │      │  │  │
     │                      │  │  │  │  │  MySQL Svc   │   │  │                     │      │  │  │
     │                      │  │  │  │  │  :3306       │   │  │                     │      │  │  │
     │                      │  │  │  │  │  sg-rds      │   │  │                     │      │  │  │
     │                      │  │  │  │  └──────┬───────┘   │  │                     │      │  │  │
     │                      │  │  │  │         │ NFS :2049 │  │                     │      │  │  │
     │                      │  │  │  │         ▼           │  │         ▼           │      │  │  │
     │                      │  │  │  │  ┌──────────────┐   │  │  ┌──────────────┐   │      │  │  │
     │                      │  │  │  │  │  EFS Mount   │   │  │  │  EFS Mount   │   │      │  │  │
     │                      │  │  │  │  │  Target      │   │  │  │  Target      │   │      │  │  │
     │                      │  │  │  │  │  sg-efs      │   │  │  │  sg-efs      │   │      │  │  │
     │                      │  │  │  │  └──────┬───────┘   │  │  └──────┬───────┘   │      │  │  │
     │                      │  │  │  └─────────┼───────────┘  └─────────┼───────────┘      │  │  │
     │                      │  │  │            │                        │                   │  │  │
     │                      │  │  │            └───────────┬────────────┘                   │  │  │
     │                      │  │  │                        ▼                                │  │  │
     │                      │  │  │               ┌────────────────┐                        │  │  │
     │                      │  │  │               │  EFS File Sys  │                        │  │  │
     │                      │  │  │               │  /mysql-data   │                        │  │  │
     │                      │  │  │               └────────────────┘                        │  │  │
     │                      │  │  └─────────────────────────────────────────────────────────┘  │  │
     │                      │  └───────────────────────────────────────────────────────────────┘  │
     │                      │                                                                     │
     │   ECR image pull     │  ┌──────────────────────┐  ┌──────────────────────┐                 │
     │   (via NAT)          │  │  ECR: sorterra-api    │  │  ECR: sorterra-mysql │                 │
     │                      │  └──────────────────────┘  └──────────────────────┘                 │
     │                      │                                                                     │
     │   Logs               │  ┌──────────────────────┐  ┌──────────────────────┐                 │
     │                      │  │  CW: /ecs/sorterra-  │  │  CW: /ecs/sorterra-  │                 │
     │                      │  │      api              │  │      mysql           │                 │
     │                      │  └──────────────────────┘  └──────────────────────┘                 │
     │                      │                                                                     │
     │   DNS resolution     │  ┌──────────────────────────────────────┐                           │
     └──────────────────────│  │  Cloud Map: sorterra.local           │                           │
                            │  │  mysql.sorterra.local → MySQL task IP│                           │
                            │  └──────────────────────────────────────┘                           │
                            └─────────────────────────────────────────────────────────────────────┘
```

## VPC and Networking

### VPC

| Property | Value |
|----------|-------|
| Name | `sorterra-Dev-vpc` |
| VPC ID | `vpc-0d3a8af5cb4da7000` |
| CIDR | `10.20.0.0/16` |
| Region | us-east-1 |

The VPC has two availability zones (us-east-1a and us-east-1b) with public and private subnets in each.

### Subnets

| Name | Subnet ID | CIDR | AZ | Tier | Auto-assign public IP |
|------|-----------|------|----|------|-----------------------|
| `sorterra-Dev-public-us-east-1a` | `subnet-0de55b71e47a519b8` | `10.20.0.0/24` | us-east-1a | Public | Yes |
| `sorterra-Dev-public-us-east-1b` | `subnet-0bfbc0de171bcb5e7` | `10.20.1.0/24` | us-east-1b | Public | Yes |
| `sorterra-Dev-private-us-east-1a` | `subnet-0c494af6d467e6dbb` | `10.20.10.0/24` | us-east-1a | Private | No |
| `sorterra-Dev-private-us-east-1b` | `subnet-05254ca568d47c8ea` | `10.20.11.0/24` | us-east-1b | Private | No |

### Routing

**Public subnets** route `0.0.0.0/0` through the internet gateway (`igw-0dd684804fa2e5b81`), giving direct internet access to the ALB.

**Private subnets** route `0.0.0.0/0` through the NAT gateway (`nat-0c9800f017de238da`, Elastic IP `98.91.120.32`), which sits in the public subnet in us-east-1a. This allows containers in private subnets to make outbound requests (pull ECR images, call external APIs) without being directly reachable from the internet.

| Route Table | Destination | Target | Associated Subnets |
|-------------|-------------|--------|--------------------|
| `sorterra-Dev-rt-public` | `0.0.0.0/0` | Internet Gateway | Both public subnets |
| `sorterra-Dev-rt-private` | `0.0.0.0/0` | NAT Gateway | Both private subnets |

## Security Groups

All security groups are in the Sorterra VPC. Traffic flows through them in a chain: Internet → ALB → API → Database.

### Traffic Flow

```
Internet ──► sg-alb ──► sg-api ──► sg-rds ──► sg-efs
             :80/443     :8080      :3306      :2049
```

### Rules

#### `sorterra-Dev-sg-alb` (`sg-095bbfe1f473c119d`)

The ALB security group. Only internet-facing entry point.

| Direction | Port | Protocol | Source/Destination | Purpose |
|-----------|------|----------|--------------------|---------|
| Inbound | 80 | TCP | `0.0.0.0/0` | HTTP from internet |
| Inbound | 443 | TCP | `0.0.0.0/0` | HTTPS from internet |
| Outbound | 8080 | TCP | `sg-api` | Forward to API containers |

#### `sorterra-Dev-sg-api` (`sg-0d557c9256b77a88b`)

The API container security group. Only accepts traffic from the ALB.

| Direction | Port | Protocol | Source/Destination | Purpose |
|-----------|------|----------|--------------------|---------|
| Inbound | 8080 | TCP | `sg-alb` | Traffic from ALB |
| Outbound | All | All | `0.0.0.0/0` | Internet via NAT (ECR, external APIs) |

#### `sorterra-Dev-sg-rds` (`sg-07251ea351d2a9d49`)

The database security group. Used by the MySQL Fargate container. Only accepts traffic from API and Jobs containers.

| Direction | Port | Protocol | Source/Destination | Purpose |
|-----------|------|----------|--------------------|---------|
| Inbound | 3306 | TCP | `sg-api` | MySQL from API container |
| Inbound | 5432 | TCP | `sg-api`, `sg-jobs` | Postgres (reserved for future RDS) |
| Outbound | All | All | `0.0.0.0/0` | Internet via NAT (ECR image pull) |

#### `sorterra-Dev-sg-efs` (`sg-011e80d4756b62bfa`)

The EFS security group. Allows NFS mounts from the database container.

| Direction | Port | Protocol | Source/Destination | Purpose |
|-----------|------|----------|--------------------|---------|
| Inbound | 2049 | TCP | `sg-rds` | NFS from MySQL container |

#### `sorterra-Dev-sg-jobs` (`sg-0a305945a9da513ef`)

Reserved for background job containers (not yet in use).

| Direction | Port | Protocol | Source/Destination | Purpose |
|-----------|------|----------|--------------------|---------|
| Inbound | — | — | — | No inbound access |
| Outbound | All | All | `0.0.0.0/0` | Internet via NAT |

## Compute — ECS Fargate

### Cluster

| Property | Value |
|----------|-------|
| Name | `sorterra` |
| Region | us-east-1 |

### API Service

| Property | Value |
|----------|-------|
| Service name | `sorterra-api` |
| Task definition | `sorterra-api:1` |
| Launch type | Fargate |
| Desired count | 1 |
| CPU | 0.25 vCPU (256 units) |
| Memory | 512 MB |
| Container port | 8080 |
| Image | `896170900648.dkr.ecr.us-east-1.amazonaws.com/sorterra-api:latest` |
| Subnets | Private 1a, Private 1b |
| Security group | `sg-api` |
| Public IP | Disabled |
| ECS Exec | Enabled |

The API container runs ASP.NET Core (.NET 10) and connects to MySQL via the Cloud Map DNS name `mysql.sorterra.local:3306`. It exposes port 8080 for HTTP traffic from the ALB.

**Environment variables:**

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment (`Development`) |
| `ASPNETCORE_URLS` | Listen address (`http://+:8080`) |
| `ConnectionStrings__DefaultConnection` | MySQL connection string (uses `mysql.sorterra.local`) |
| `Encryption__TokenEncryptionKey` | Key for encrypting OAuth tokens |

**Health check:** `curl -f http://localhost:8080/health/live` every 30s (15s start period).

### MySQL Service

| Property | Value |
|----------|-------|
| Service name | `sorterra-mysql` |
| Task definition | `sorterra-mysql:1` |
| Launch type | Fargate |
| Desired count | 1 |
| CPU | 0.5 vCPU (512 units) |
| Memory | 1024 MB |
| Container port | 3306 |
| Image | `896170900648.dkr.ecr.us-east-1.amazonaws.com/sorterra-mysql:latest` |
| Subnets | Private 1a |
| Security group | `sg-rds` |
| Public IP | Disabled |
| ECS Exec | Enabled |
| Volume | EFS `fs-0969642d4386d5187` mounted at `/var/lib/mysql` |

The MySQL 8.0 container has the database schema and seed data baked into its image (via init scripts in `/docker-entrypoint-initdb.d/`). Data is persisted on EFS.

**Health check:** `mysqladmin ping` every 30s (120s start period to allow for initial database initialization).

## Load Balancer

| Property | Value |
|----------|-------|
| Name | `sorterra-alb` |
| Type | Application Load Balancer |
| Scheme | Internet-facing |
| DNS | `sorterra-alb-914783819.us-east-1.elb.amazonaws.com` |
| Subnets | Public 1a, Public 1b |
| Security group | `sg-alb` |

### Listener

| Port | Protocol | Action |
|------|----------|--------|
| 80 | HTTP | Forward to `sorterra-api-tg` |

### Target Group: `sorterra-api-tg`

| Property | Value |
|----------|-------|
| Target type | IP (required for Fargate `awsvpc` networking) |
| Protocol | HTTP |
| Port | 8080 |
| Health check path | `/health/live` |
| Health check interval | 30s |
| Healthy threshold | 2 consecutive checks |
| Unhealthy threshold | 3 consecutive checks |

ECS automatically registers and deregisters API task IPs in this target group as tasks start and stop.

## Storage — EFS

| Property | Value |
|----------|-------|
| File System ID | `fs-0969642d4386d5187` |
| Name | `sorterra-Dev-efs-mysql` |
| Performance mode | General Purpose |
| Throughput mode | Bursting |
| Encrypted | No |

### Access Point

| Property | Value |
|----------|-------|
| Access Point ID | `fsap-01f4a55db8f71ee26` |
| Root directory | `/mysql-data` |
| POSIX UID/GID | `999:999` (the `mysql` user inside the container) |

### Mount Targets

EFS has a mount target in each private subnet so the MySQL container can access the volume regardless of which AZ it runs in.

| Subnet | Mount Target |
|--------|-------------|
| Private 1a (`subnet-0c494af6d467e6dbb`) | `fsmt-01659f4f1bac80e76` |
| Private 1b (`subnet-05254ca568d47c8ea`) | `fsmt-049075e6c17306e6d` |

## Service Discovery — Cloud Map

| Property | Value |
|----------|-------|
| Namespace | `sorterra.local` (private DNS, `ns-v273mmw6w46poowr`) |
| MySQL service | `mysql.sorterra.local` (`srv-lx75oz2wnk2josq4`) |
| Record type | A (IP address) |
| TTL | 10 seconds |

When the MySQL Fargate task starts, ECS registers its private IP with Cloud Map. The API container resolves `mysql.sorterra.local` to that IP via the VPC's private hosted zone. If the MySQL task restarts and gets a new IP, Cloud Map updates the DNS record automatically.

## Container Registry — ECR

| Repository | URI |
|------------|-----|
| `sorterra-api` | `896170900648.dkr.ecr.us-east-1.amazonaws.com/sorterra-api` |
| `sorterra-mysql` | `896170900648.dkr.ecr.us-east-1.amazonaws.com/sorterra-mysql` |

Both images are built with `--platform linux/amd64` for Fargate compatibility. The API image is a multi-stage .NET 10 build. The MySQL image extends `mysql:8.0` with custom config and init scripts.

## IAM Roles

### Task Execution Role — `sorterra-ecs-execution-role`

Used by the ECS agent (not your application code) to pull images from ECR and write logs to CloudWatch.

| Policy | Purpose |
|--------|---------|
| `AmazonECSTaskExecutionRolePolicy` (AWS managed) | ECR pull, CloudWatch Logs |

### Task Role — `sorterra-ecs-task-role`

Used by your application code running inside the container. Attach policies here when the API needs to call other AWS services.

| Policy | Purpose |
|--------|---------|
| `AmazonSSMManagedInstanceCore` (AWS managed) | ECS Exec (interactive shell into containers) |

## Logging — CloudWatch

| Log Group | Source |
|-----------|--------|
| `/ecs/sorterra-api` | API container stdout/stderr |
| `/ecs/sorterra-mysql` | MySQL container stdout/stderr |

Both use the `awslogs` log driver. Stream names follow the pattern `{prefix}/{container-name}/{task-id}`.

View logs:

```bash
# API logs (last 30 minutes, streaming)
aws logs tail /ecs/sorterra-api --since 30m --follow --region us-east-1

# MySQL logs
aws logs tail /ecs/sorterra-mysql --since 30m --follow --region us-east-1
```

## Request Flow

A request from the internet to the API follows this path:

```
1. Client sends request to sorterra-alb-914783819.us-east-1.elb.amazonaws.com
2. DNS resolves to the ALB's public IP in one of the public subnets
3. ALB listener on port 80 receives the request
4. ALB forwards to a healthy target in sorterra-api-tg (API container IP on port 8080)
5. Traffic crosses from public subnet (ALB) to private subnet (API) within the VPC
6. API container processes the request
7. If the request needs data, the API connects to mysql.sorterra.local:3306
8. Cloud Map resolves mysql.sorterra.local to the MySQL container's private IP
9. MySQL reads/writes data on the EFS volume mounted at /var/lib/mysql
10. Response flows back: MySQL → API → ALB → Client
```

## Cost Estimate

Approximate monthly cost running 24/7 in us-east-1:

| Resource | Spec | Est. Cost/Month |
|----------|------|-----------------|
| Fargate — API | 0.25 vCPU, 512 MB | ~$9 |
| Fargate — MySQL | 0.5 vCPU, 1 GB | ~$18 |
| ALB | Base + LCU | ~$16 + traffic |
| NAT Gateway | Base + data | ~$32 + $0.045/GB |
| EFS | Storage + I/O | ~$1–3 |
| CloudWatch Logs | Ingestion + storage | ~$1–2 |
| **Total** | | **~$77–80/month** |

To reduce cost when not in use, scale services to 0:

```bash
aws ecs update-service --cluster sorterra --service sorterra-api --desired-count 0 --region us-east-1
aws ecs update-service --cluster sorterra --service sorterra-mysql --desired-count 0 --region us-east-1
```

The NAT gateway and ALB still incur charges even with services scaled down. To fully stop costs, delete these resources and recreate them when needed (see [cleanup in the deployment guide](aws-ecs-fargate-deployment.md#cleanup)).
