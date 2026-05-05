# OCN E-Wallet — Developer Guide
### Testing · Running · Hosting on a Budget

> **Stack recap:** ASP.NET Core 8 API · Blazor WASM · PostgreSQL · Redis · Hangfire · SignalR · JWT

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Running Locally](#2-running-locally)
3. [What to Test & How](#3-what-to-test--how)
4. [Running the Test Suite](#4-running-the-test-suite)
5. [End-to-End Manual QA Checklist](#5-end-to-end-manual-qa-checklist)
6. [Hosting on Minimal Cost](#6-hosting-on-minimal-cost)
7. [Environment Variables Reference](#7-environment-variables-reference)
8. [Monitoring & Observability](#8-monitoring--observability)
9. [CI/CD (Free Tier)](#9-cicd-free-tier)
10. [Cost Summary](#10-cost-summary)
11. [Hosting on AWS (Production Scale)](#11-hosting-on-aws-production-scale)
12. [ScyllaDB Integration (C#)](#12-scylladb-integration-c)

---

## 1. Prerequisites

| Tool | Minimum Version | Install |
|------|----------------|---------|
| .NET SDK | 8.0 | https://dotnet.microsoft.com/download |
| Docker Desktop | 24+ | https://docs.docker.com/get-docker/ |
| Node.js *(optional, for tools)* | 18 LTS | https://nodejs.org |
| `dotnet-ef` CLI | 8.0 | `dotnet tool install -g dotnet-ef` |

```bash
# Verify everything is in place
dotnet --version          # should print 8.x.x
docker --version
dotnet ef --version
```

---

## 2. Running Locally

### 2.1 Start Infrastructure

```bash
# From repo root
docker-compose up -d postgres redis

# Confirm both containers are healthy
docker ps --format "table {{.Names}}\t{{.Status}}"
```

Expected output:
```
ocn_postgres   Up X seconds (healthy)
ocn_redis      Up X seconds
```

### 2.2 Apply Migrations

```bash
dotnet ef database update \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API
```

### 2.3 Run the API

```bash
dotnet run --project src/EWallet.API

# Swagger UI → http://localhost:5000/swagger
# Health check → http://localhost:5000/health
# SignalR Hub  → ws://localhost:5000/hubs/wallet
```

### 2.4 Run the Blazor Frontend

Open a **second terminal:**

```bash
dotnet run --project src/EWallet.BlazorClient
# App → http://localhost:5001
```

### 2.5 Hangfire Dashboard (local only)

```
http://localhost:5000/hangfire
```

> ⚠️ Lock down the Hangfire dashboard with `[Authorize(Roles = "Admin")]` before any public deployment.

---

## 3. What to Test & How

The suite is divided into three layers. Run them in order — each layer depends on the one above it.

### Layer 1 — Domain Tests (`tests/EWallet.Domain.Tests`)

**What they cover:**

- `Wallet` balance mutation methods (`Debit`, `Credit`) — happy path and guard clauses
- Tier daily limit enforcement (Tier1=$1,000 / Tier2=$5,000 / Tier3=unlimited)
- Optimistic concurrency: `RowVersion` increments on every mutation
- Domain events are raised correctly (e.g., `FundsTransferredEvent`)
- Value objects (`Money`, `Currency`) equality and validation
- Idempotency key format validation

**Tools:** xUnit · FluentAssertions · no I/O needed

**Key scenarios to assert:**

```
✅ Debit reduces balance by exact amount
✅ Credit increases balance by exact amount
✅ Debit below zero throws DomainException (insufficient funds)
✅ Debit exceeding daily Tier1 limit throws DomainException
✅ Money.Amount is always rounded to 2 decimal places
✅ RowVersion increments after each mutation
✅ Transfer domain event carries correct wallet IDs and amount
```

---

### Layer 2 — Application Tests (`tests/EWallet.Application.Tests`)

**What they cover:**

- MediatR command/query handlers (mocked repositories)
- FluentValidation rules fire correctly on invalid input
- `Result<T>` propagation — errors never throw across layer boundaries
- 2FA enforcement triggers for transfers > $500
- Idempotency: duplicate command with same key returns cached result, not a double-write
- AuditLog entry is written on every Debit, Credit, and status change

**Tools:** xUnit · Moq · FluentAssertions · MediatR test harness

**Key scenarios to assert:**

```
✅ TransferCommand with amount > $500 and no 2FA code → Result.Failure("2FA required")
✅ TransferCommand with duplicate idempotency key → same TxId returned, no new DB write
✅ TransferCommand with negative amount → ValidationException raised before handler runs
✅ GetWalletQuery returns opaque ExternalId, never internal int ID
✅ AuditLog repository receives exactly one Write() call per balance mutation
✅ Daily limit validator calls IWalletRepository.GetDailyTotalAsync with correct date range
```

---

### Layer 3 — Integration Tests (`tests/EWallet.Integration.Tests`)

**What they cover:**

- Full HTTP request → PostgreSQL → response cycle using `WebApplicationFactory<Program>`
- SignalR hub emits `BalanceUpdated` event to the right user after a transfer
- Pessimistic locking: two concurrent transfers > $1,000 to same wallet — only one proceeds, second blocks then succeeds (no double-spend)
- Redis cache invalidation after a balance mutation
- Hangfire job fires and completes `ProcessPendingSettlementsJob`
- Rate limiting: 11th wallet operation within 60 s returns HTTP 429
- JWT expiry + refresh token rotation happy path

**Tools:** xUnit · WebApplicationFactory · Testcontainers (Postgres + Redis) · Microsoft.AspNetCore.SignalR.Client

**Docker required** — Testcontainers spins up ephemeral containers automatically.

```bash
# Make sure Docker daemon is running before this layer
docker info
```

**Key scenarios to assert:**

```
✅ POST /api/auth/register → 201, JWT + refresh cookie set
✅ POST /api/wallets/transfer (valid) → 200, balance updated, SignalR event fired
✅ POST /api/wallets/transfer (> $500, missing 2FA) → 403
✅ POST /api/wallets/transfer (> $1,000) concurrent × 2 → no race condition
✅ GET /api/wallets/{id} with expired JWT → 401, use refresh endpoint → new JWT issued
✅ POST /api/wallets/transfer × 11 within 60 s → 429 on 11th
✅ GET /health → 200 with db/redis/hangfire all "Healthy"
```

---

## 4. Running the Test Suite

```bash
# Domain tests (no Docker needed)
dotnet test tests/EWallet.Domain.Tests --logger "console;verbosity=normal"

# Application tests (no Docker needed)
dotnet test tests/EWallet.Application.Tests --logger "console;verbosity=normal"

# Integration tests (Docker required)
dotnet test tests/EWallet.Integration.Tests \
  --logger "console;verbosity=normal" \
  -- RunConfiguration.TestSessionTimeout=120000

# Run all layers at once with coverage
dotnet test \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

# Generate HTML coverage report (requires reportgenerator tool)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" \
  -reporttypes:Html

open TestResults/CoverageReport/index.html
```

**Minimum acceptable coverage targets:**

| Project | Line Coverage |
|---------|--------------|
| EWallet.Domain | ≥ 90% |
| EWallet.Application | ≥ 80% |
| EWallet.Infrastructure | ≥ 60% |

---

## 5. End-to-End Manual QA Checklist

Use Swagger UI (`/swagger`) or a REST client (Insomnia / Postman).

### Auth Flow

- [ ] Register a new user → receive JWT + `Set-Cookie: refreshToken`
- [ ] Call a protected endpoint without JWT → `401 Unauthorized`
- [ ] Call `POST /api/auth/refresh` with the HttpOnly cookie → new JWT issued, old refresh token invalidated
- [ ] Call `POST /api/auth/refresh` with the old (rotated) token → `401` (rotation worked)

### Wallet Operations

- [ ] Create wallet for authenticated user
- [ ] Credit $200 → balance updates, AuditLog entry written
- [ ] Debit $50 → balance updates
- [ ] Attempt debit of $999,999 → `400` insufficient funds
- [ ] Transfer $600 without 2FA code → `403`
- [ ] Transfer $600 with valid 2FA code → `200`, both wallets update atomically
- [ ] Transfer $1,200 → pessimistic lock engaged (check API logs for `SELECT FOR UPDATE`)
- [ ] Send same transfer request twice with identical idempotency key → second call returns same `transactionId`, balance not double-debited

### Rate Limiting

- [ ] Fire 11 wallet requests within 60 seconds → 11th returns `429 Too Many Requests`

### SignalR Live Updates

1. Open two browser tabs, both logged in as the same user
2. Execute a transfer in tab 1
3. Confirm balance updates in real time in tab 2 without a page refresh

### Health Check

```bash
curl http://localhost:5000/health | jq .
```

Expected: all three components `Healthy`.

---

## 6. Hosting on Minimal Cost

The goal: production-grade deployment under **~$10–15/month**.

### Recommended Stack (cheapest viable)

| Component | Service | Monthly Cost |
|-----------|---------|-------------|
| API + Blazor host | [Fly.io](https://fly.io) Shared-CPU-1x 256 MB | ~$1.94 |
| PostgreSQL | [Neon.tech](https://neon.tech) Free tier (0.5 GB) | **$0** |
| Redis | [Upstash](https://upstash.com) Free tier (10k cmd/day) | **$0** |
| Container registry | GitHub Container Registry | **$0** |
| CI/CD | GitHub Actions (2,000 min/month free) | **$0** |
| TLS/CDN | Cloudflare Free | **$0** |
| Logging/Tracing | [Seq Cloud](https://datalust.co/seq) Free (1 GB/day) | **$0** |
| Domain | Namecheap .com | ~$10/year |
| **Total** | | **~$2–3/month** |

> Scale up: if you outgrow Neon free tier, move to Neon Pro ($19/mo) or Supabase ($25/mo). Upstash paid starts at $0.20 per 100k commands.

---

### 6.1 Dockerize the API

Create `src/EWallet.API/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/EWallet.API/EWallet.API.csproj", "src/EWallet.API/"]
COPY ["src/EWallet.Application/EWallet.Application.csproj", "src/EWallet.Application/"]
COPY ["src/EWallet.Domain/EWallet.Domain.csproj", "src/EWallet.Domain/"]
COPY ["src/EWallet.Infrastructure/EWallet.Infrastructure.csproj", "src/EWallet.Infrastructure/"]
RUN dotnet restore "src/EWallet.API/EWallet.API.csproj"
COPY . .
RUN dotnet publish "src/EWallet.API/EWallet.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EWallet.API.dll"]
```

### 6.2 Dockerize the Blazor Frontend

Blazor WASM compiles to **static files** — host them cheaply on any static CDN.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/EWallet.BlazorClient/ .
RUN dotnet publish -c Release -o /app/publish

FROM nginx:alpine AS final
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
```

`nginx.conf` (handles Blazor client-side routing):

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

> **Cheapest option:** Push the `wwwroot` folder to a GitHub Pages branch or Cloudflare Pages — completely free static hosting.

### 6.3 Deploy API to Fly.io

```bash
# Install Fly CLI
curl -L https://fly.io/install.sh | sh

# Authenticate
fly auth login

# Launch from repo root (follow prompts, choose nearest region)
fly launch --dockerfile src/EWallet.API/Dockerfile --name ocn-api

# Set secrets (do NOT commit these to git)
fly secrets set \
  ConnectionStrings__DefaultConnection="<neon-postgres-url>" \
  Redis__ConnectionString="<upstash-redis-url>" \
  Jwt__SecretKey="<256-bit-random-string>" \
  Jwt__Issuer="https://ocn-api.fly.dev" \
  Jwt__Audience="https://ocn.pages.dev"

# Deploy
fly deploy

# Check logs
fly logs
```

### 6.4 Deploy Blazor Frontend to Cloudflare Pages

1. Push your repo to GitHub
2. In Cloudflare Pages → **New Project** → connect GitHub repo
3. Set build command: `dotnet publish src/EWallet.BlazorClient -c Release -o dist`
4. Set output directory: `dist/wwwroot`
5. Add environment variable: `API_BASE_URL=https://ocn-api.fly.dev`

Free tier: unlimited bandwidth, 500 builds/month.

### 6.5 Run Migrations in Production

```bash
# Via Fly.io one-off machine
fly ssh console --command \
  "dotnet ef database update --project /app --connection \"$ConnectionStrings__DefaultConnection\""
```

Or add a migration runner to `Program.cs` behind an environment flag:

```csharp
// Program.cs — only in non-production or on first boot
if (app.Environment.IsStaging() || Environment.GetEnvironmentVariable("RUN_MIGRATIONS") == "true")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

### 6.6 Securing the Deployment

```bash
# Generate a strong JWT secret
openssl rand -base64 64

# Never commit appsettings.Production.json — use fly secrets or equivalent
```

- Enable `HTTPS only` in Fly.io dashboard
- Set `Secure; HttpOnly; SameSite=Strict` on refresh token cookie
- Lock CORS in `appsettings.Production.json`:

```json
"Cors": {
  "AllowedOrigins": ["https://ocn.pages.dev"]
}
```

- Set Hangfire dashboard to `[Authorize(Roles = "Admin")]`
- Set Swagger UI to disabled in Production:

```csharp
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

---

## 7. Environment Variables Reference

| Variable | Example | Where to set |
|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | `Host=…;Database=ocn;Username=…;Password=…` | Fly secrets / `.env` |
| `Redis__ConnectionString` | `redis://default:…@…upstash.io:6379` | Fly secrets |
| `Jwt__SecretKey` | `<64-char base64>` | Fly secrets |
| `Jwt__Issuer` | `https://ocn-api.fly.dev` | appsettings.Production.json |
| `Jwt__Audience` | `https://ocn.pages.dev` | appsettings.Production.json |
| `Jwt__AccessTokenExpiryMinutes` | `15` | appsettings.json |
| `Jwt__RefreshTokenExpiryDays` | `7` | appsettings.json |
| `Hangfire__DashboardPath` | `/hangfire` | appsettings.json |
| `Serilog__SeqUrl` | `https://…seq.datalust.co` | Fly secrets |
| `RateLimiting__WalletOpsPerMinute` | `10` | appsettings.json |
| `RUN_MIGRATIONS` | `true` (first deploy only) | Fly secrets (remove after) |

---

## 8. Monitoring & Observability

### Structured Logging (Serilog → Seq)

All logs include `UserId`, `WalletId`, `TraceId`, `TransactionId` as structured properties. To query in Seq:

```
# All failed transfers
EventType = "TransferFailed"

# All 2FA blocks
EventType = "TwoFactorRequired" AND Amount > 500

# Slow queries (> 500 ms)
ElapsedMs > 500 AND SourceContext like '%Repository%'
```

### Health Check Response

```bash
curl https://ocn-api.fly.dev/health | jq .
```

```json
{
  "status": "Healthy",
  "results": {
    "database": { "status": "Healthy" },
    "redis":    { "status": "Healthy" },
    "hangfire": { "status": "Healthy" }
  }
}
```

### Alerting (free)

Set up [Betterstack Uptime](https://betterstack.com/uptime) (free tier) to ping `/health` every 3 minutes and alert on Slack/email if it returns anything other than `200 Healthy`.

---

## 9. CI/CD (Free Tier)

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_DB: ocn_test
          POSTGRES_USER: ocn
          POSTGRES_PASSWORD: ocn
        ports: ["5432:5432"]
        options: --health-cmd pg_isready --health-interval 5s --health-timeout 5s --health-retries 5
      redis:
        image: redis:7
        ports: ["6379:6379"]

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Domain + Application Tests
        run: |
          dotnet test tests/EWallet.Domain.Tests --no-build -c Release
          dotnet test tests/EWallet.Application.Tests --no-build -c Release

      - name: Integration Tests
        env:
          ConnectionStrings__DefaultConnection: "Host=localhost;Database=ocn_test;Username=ocn;Password=ocn"
          Redis__ConnectionString: "localhost:6379"
        run: dotnet test tests/EWallet.Integration.Tests --no-build -c Release

  deploy:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: superfly/flyctl-actions/setup-flyctl@master
      - run: flyctl deploy --remote-only
        env:
          FLY_API_TOKEN: ${{ secrets.FLY_API_TOKEN }}
```

Add `FLY_API_TOKEN` to GitHub repo **Settings → Secrets**.

---

## 10. Cost Summary

| Scenario | Monthly Cost |
|----------|-------------|
| Development (local Docker only) | **$0** |
| Solo/hobby production (Fly free allowance + Neon free + Upstash free) | **$0 – $2** |
| Small production (Fly shared-1x + Neon free + Upstash free) | **~$2 – $5** |
| Growing app (Fly + Neon Starter $19 + Upstash Pay-as-you-go) | **~$25 – $40** |
| Full production (Fly Performance + Neon Pro + Upstash Pro) | **~$60 – $100** |

> Start on the free tiers. Only upgrade when you hit limits — Neon and Upstash both have clear usage dashboards so you'll know exactly when.

---

## 11. Hosting on AWS (Production Scale)

This section covers deploying OCN on AWS using services that are either **free tier eligible** for the first 12 months or genuinely cost-effective at scale. The architecture below maps directly to the existing stack.

### 11.1 AWS Service Mapping

| OCN Component | AWS Service | Free Tier |
|---------------|-------------|-----------|
| API container | **ECS Fargate** (via App Runner or ECS) | App Runner: 1M req/mo free |
| Blazor WASM static files | **S3 + CloudFront** | 5 GB S3, 1 TB CF transfer/mo |
| PostgreSQL | **RDS PostgreSQL** (db.t3.micro) | 750 hrs/mo free (12 months) |
| Redis | **ElastiCache Serverless** or **MemoryDB** | No free tier — use Upstash Redis for AWS region instead |
| Background jobs (Hangfire) | Runs inside ECS task — no extra service needed | — |
| SignalR | Runs inside ECS task — or **AWS API Gateway WebSocket** for scale | 1M messages/mo free |
| Secrets | **AWS Secrets Manager** or **SSM Parameter Store** | SSM Standard is free |
| Container images | **Amazon ECR** | 500 MB/mo free |
| TLS + CDN | **CloudFront + ACM** (free TLS cert) | Free |
| Logs | **CloudWatch Logs** | 5 GB ingestion/mo free |
| CI/CD | **GitHub Actions → ECR → ECS** | GitHub free tier |

> **Cheapest AWS production setup (post-free-tier):** ~$25–50/month for light traffic using App Runner + RDS t3.micro + S3/CloudFront + SSM.

---

### 11.2 Push Docker Image to ECR

```bash
# Authenticate Docker to ECR
aws ecr get-login-password --region ap-south-1 | \
  docker login --username AWS --password-stdin \
  <account-id>.dkr.ecr.ap-south-1.amazonaws.com

# Create the repo (once)
aws ecr create-repository --repository-name ocn-api --region ap-south-1

# Build and push
docker build -t ocn-api -f src/EWallet.API/Dockerfile .
docker tag ocn-api:latest <account-id>.dkr.ecr.ap-south-1.amazonaws.com/ocn-api:latest
docker push <account-id>.dkr.ecr.ap-south-1.amazonaws.com/ocn-api:latest
```

---

### 11.3 Deploy API with AWS App Runner (Simplest)

App Runner is the lowest-ops way to run a container on AWS — no ECS cluster, no load balancer config. It auto-scales to zero between requests.

```bash
# Create App Runner service via CLI
aws apprunner create-service \
  --service-name ocn-api \
  --source-configuration '{
    "ImageRepository": {
      "ImageIdentifier": "<account-id>.dkr.ecr.ap-south-1.amazonaws.com/ocn-api:latest",
      "ImageRepositoryType": "ECR",
      "ImageConfiguration": {
        "Port": "8080",
        "RuntimeEnvironmentVariables": {
          "ASPNETCORE_ENVIRONMENT": "Production"
        }
      }
    },
    "AutoDeploymentsEnabled": true,
    "AuthenticationConfiguration": {
      "AccessRoleArn": "arn:aws:iam::<account-id>:role/AppRunnerECRAccessRole"
    }
  }' \
  --instance-configuration '{"Cpu": "0.25 vCPU", "Memory": "0.5 GB"}'
```

Inject secrets at runtime via SSM (not env vars in code):

```bash
# Store secrets
aws ssm put-parameter --name "/ocn/prod/jwt-secret" \
  --value "<your-secret>" --type SecureString

aws ssm put-parameter --name "/ocn/prod/db-connection" \
  --value "Host=…" --type SecureString
```

Read them in `Program.cs` using the AWS SSM provider:

```csharp
// Install: dotnet add package AWSSDK.Extensions.NETCore.Setup
// Install: dotnet add package Amazon.Extensions.Configuration.SystemsManager

builder.Configuration.AddSystemsManager("/ocn/prod/", new AWSOptions
{
    Region = RegionEndpoint.APSouth1
});
```

---

### 11.4 Host Blazor WASM on S3 + CloudFront

```bash
# Create S3 bucket (replace with your domain name)
aws s3 mb s3://ocn-app-frontend --region ap-south-1

# Build Blazor
dotnet publish src/EWallet.BlazorClient -c Release -o dist

# Sync to S3
aws s3 sync dist/wwwroot s3://ocn-app-frontend --delete

# Invalidate CloudFront cache after each deploy
aws cloudfront create-invalidation \
  --distribution-id <CF_DIST_ID> \
  --paths "/*"
```

In your CloudFront distribution, add a **Custom Error Response**: HTTP 404 → `/index.html` with 200 status. This handles Blazor's client-side routing.

---

### 11.5 RDS PostgreSQL Setup

```bash
# Create a t3.micro instance (free tier eligible for 12 months)
aws rds create-db-instance \
  --db-instance-identifier ocn-db \
  --db-instance-class db.t3.micro \
  --engine postgres \
  --engine-version 16 \
  --master-username ocnadmin \
  --master-user-password <strong-password> \
  --allocated-storage 20 \
  --no-publicly-accessible \
  --vpc-security-group-ids <sg-id> \
  --db-name ocn
```

> Keep RDS in a private subnet. App Runner accesses it via VPC connector. Never expose port 5432 to the internet.

Run migrations as a one-off ECS task or via a GitHub Actions step using an SSM Session Manager tunnel — no bastion host needed.

---

### 11.6 Redis on AWS

ElastiCache has **no free tier**. The two practical options:

**Option A — Upstash Redis (recommended for budget):** Upstash supports AWS regions natively. Create a database in `ap-south-1` and connect from App Runner with zero egress cost within the same region.

**Option B — MemoryDB for Redis:** Full Redis-compatible, durable, HA. Starts at ~$25/month for a `db.t4g.small`. Use this if you need persistence guarantees beyond what a cache offers.

```csharp
// appsettings.Production.json
"Redis": {
  "ConnectionString": "redis://default:<token>@<upstash-host>.upstash.io:6379,ssl=True,abortConnect=False"
}
```

---

### 11.7 GitHub Actions → ECR → App Runner (Auto Deploy)

Add this job to your existing `.github/workflows/ci.yml` after the `test` job:

```yaml
  deploy-aws:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions:
      id-token: write
      contents: read

    steps:
      - uses: actions/checkout@v4

      - name: Configure AWS credentials (OIDC — no long-lived keys)
        uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: arn:aws:iam::<account-id>:role/GitHubActionsDeployRole
          aws-region: ap-south-1

      - name: Login to ECR
        uses: aws-actions/amazon-ecr-login@v2

      - name: Build and push image
        run: |
          IMAGE_URI=<account-id>.dkr.ecr.ap-south-1.amazonaws.com/ocn-api:${{ github.sha }}
          docker build -t $IMAGE_URI -f src/EWallet.API/Dockerfile .
          docker push $IMAGE_URI
          echo "IMAGE_URI=$IMAGE_URI" >> $GITHUB_ENV

      - name: Deploy to App Runner
        run: |
          aws apprunner start-deployment --service-arn <app-runner-service-arn>
```

> Use **OIDC federation** (the `id-token: write` permission) — never store `AWS_ACCESS_KEY_ID` as a GitHub secret. OIDC issues short-lived tokens per job run with no static credentials to rotate or leak.

---

### 11.8 AWS Cost Estimate

| Service | Config | Monthly (post-free-tier) |
|---------|--------|--------------------------|
| App Runner | 0.25 vCPU / 0.5 GB, ~10k req/day | ~$5–10 |
| RDS PostgreSQL | db.t3.micro, 20 GB gp3 | ~$15 |
| S3 + CloudFront | 1 GB static, 10 GB transfer | ~$1 |
| Upstash Redis | Pay-per-request at low traffic | ~$0–2 |
| ECR | < 500 MB images | ~$0 |
| SSM Parameter Store | Standard tier | $0 |
| CloudWatch Logs | < 5 GB/mo | ~$0 |
| **Total** | | **~$21–28/month** |

Scale levers: move to RDS t4g.micro (ARM, ~20% cheaper), enable RDS auto-pause for dev environments, and use CloudFront caching aggressively to keep App Runner request count low.

---

## 12. ScyllaDB Integration (C#)

ScyllaDB is a drop-in Cassandra-compatible database written in C++ that delivers significantly higher throughput and lower latency than Cassandra at the same hardware cost. For OCN it's the right choice for **append-heavy, high-volume tables**: AuditLog, TransactionHistory, and NotificationEvents — where you need millions of writes per day but never update existing rows.

> **Do not replace PostgreSQL with ScyllaDB.** Keep Postgres for wallet balances and user accounts (strong consistency, ACID). Use ScyllaDB alongside it for the write-heavy append-only workloads.

### 12.1 Why ScyllaDB for OCN

| Requirement | Why ScyllaDB fits |
|-------------|------------------|
| AuditLog — every debit/credit/status change | Append-only, time-series shape, massive write volume |
| TransactionHistory — per-user query by date range | Partition by `userId`, cluster by `timestamp` |
| NotificationEvents — SignalR delivery receipts | Fire-and-forget, extremely high write rate |
| No UPDATE ever needed on history rows | Immutable log — perfect for Cassandra data model |

### 12.2 NuGet Package

The best C# driver for ScyllaDB is the **official Cassandra C# driver** (DataStax), which ScyllaDB is fully compatible with. There is also a ScyllaDB-specific fork with shard-aware routing for maximum performance.

```bash
# Option A — DataStax driver (stable, well-documented)
dotnet add package CassandraCSharpDriver

# Option B — ScyllaDB shard-aware fork (better perf on ScyllaDB specifically)
dotnet add package ScyllaDB.Driver
# (community package — check latest name on NuGet)
```

For most projects, the DataStax driver is the right call. Switch to the shard-aware fork only if profiling shows routing overhead.

---

### 12.3 Local ScyllaDB via Docker

Add to your `docker-compose.yml`:

```yaml
  scylla:
    image: scylladb/scylla:6.0
    container_name: ocn_scylla
    command: --smp 1 --memory 512M --developer-mode 1
    ports:
      - "9042:9042"   # CQL native transport
    volumes:
      - scylla_data:/var/lib/scylla
    healthcheck:
      test: ["CMD", "cqlsh", "-e", "describe keyspaces"]
      interval: 10s
      timeout: 5s
      retries: 10

volumes:
  scylla_data:
```

```bash
docker-compose up -d scylla
# Wait for healthy, then verify
docker exec -it ocn_scylla cqlsh -e "SELECT release_version FROM system.local;"
```

---

### 12.4 Keyspace and Table Schema

Connect with `cqlsh` or run these at startup:

```sql
CREATE KEYSPACE IF NOT EXISTS ocn
  WITH replication = {
    'class': 'NetworkTopologyStrategy',
    'replication_factor': 1   -- use 3 in production
  }
  AND durable_writes = true;

USE ocn;

-- AuditLog: partition by walletId, cluster newest-first
CREATE TABLE IF NOT EXISTS audit_log (
    wallet_id    UUID,
    occurred_at  TIMESTAMP,
    event_type   TEXT,       -- 'Debit' | 'Credit' | 'StatusChange'
    amount       DECIMAL,
    balance_after DECIMAL,
    actor_id     UUID,
    idempotency_key TEXT,
    metadata     TEXT,       -- JSON blob for extra context
    PRIMARY KEY (wallet_id, occurred_at)
) WITH CLUSTERING ORDER BY (occurred_at DESC)
  AND default_time_to_live = 94608000;  -- 3 years TTL

-- TransactionHistory: partition by userId for fast user-facing queries
CREATE TABLE IF NOT EXISTS transaction_history (
    user_id      UUID,
    tx_date      DATE,       -- for date-range queries
    occurred_at  TIMESTAMP,
    transaction_id UUID,
    tx_type      TEXT,
    amount       DECIMAL,
    counterparty_id UUID,
    status       TEXT,
    PRIMARY KEY ((user_id, tx_date), occurred_at)
) WITH CLUSTERING ORDER BY (occurred_at DESC);
```

---

### 12.5 C# Infrastructure Service

Create `src/EWallet.Infrastructure/ScyllaDb/ScyllaDbContext.cs`:

```csharp
using Cassandra;
using Cassandra.Mapping;
using Microsoft.Extensions.Options;

public class ScyllaDbContext : IDisposable
{
    private readonly ICluster _cluster;
    public ISession Session { get; }

    public ScyllaDbContext(IOptions<ScyllaDbOptions> options)
    {
        _cluster = Cluster.Builder()
            .AddContactPoints(options.Value.ContactPoints)
            .WithPort(options.Value.Port)
            .WithCredentials(options.Value.Username, options.Value.Password)
            // Shard-aware routing (ScyllaDB-specific, ignored by Cassandra)
            .WithLoadBalancingPolicy(new TokenAwarePolicy(new DCAwareRoundRobinPolicy()))
            .WithReconnectionPolicy(new ExponentialReconnectionPolicy(1000, 10000))
            .WithQueryOptions(new QueryOptions()
                .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                .SetDefaultIdempotence(false))
            .Build();

        Session = _cluster.Connect("ocn");
    }

    public void Dispose()
    {
        Session?.Dispose();
        _cluster?.Dispose();
    }
}

public class ScyllaDbOptions
{
    public string[] ContactPoints { get; set; } = ["localhost"];
    public int Port { get; set; } = 9042;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
```

---

### 12.6 AuditLog Repository

Create `src/EWallet.Infrastructure/ScyllaDb/ScyllaAuditLogRepository.cs`:

```csharp
using Cassandra;
using EWallet.Application.Interfaces;
using EWallet.Domain.Entities;

public class ScyllaAuditLogRepository : IAuditLogRepository
{
    private readonly ISession _session;
    private PreparedStatement? _insertStmt;

    public ScyllaAuditLogRepository(ScyllaDbContext context)
    {
        _session = context.Session;
    }

    private async Task<PreparedStatement> GetInsertStatementAsync()
    {
        return _insertStmt ??= await _session.PrepareAsync(@"
            INSERT INTO audit_log
              (wallet_id, occurred_at, event_type, amount, balance_after,
               actor_id, idempotency_key, metadata)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        ");
    }

    public async Task WriteAsync(AuditLogEntry entry)
    {
        var stmt = await GetInsertStatementAsync();

        var bound = stmt.Bind(
            entry.WalletId,
            entry.OccurredAt,
            entry.EventType.ToString(),
            entry.Amount,
            entry.BalanceAfter,
            entry.ActorId,
            entry.IdempotencyKey,
            entry.MetadataJson
        );

        // UNLOGGED batch is fine for single-partition inserts
        await _session.ExecuteAsync(bound);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetByWalletAsync(
        Guid walletId,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit = 100)
    {
        var ps = await _session.PrepareAsync(@"
            SELECT * FROM audit_log
            WHERE wallet_id = ?
              AND occurred_at >= ?
              AND occurred_at <= ?
            LIMIT ?
        ");

        var rs = await _session.ExecuteAsync(ps.Bind(walletId, from, to, limit));

        return rs.Select(row => new AuditLogEntry
        {
            WalletId      = row.GetValue<Guid>("wallet_id"),
            OccurredAt    = row.GetValue<DateTimeOffset>("occurred_at"),
            EventType     = Enum.Parse<AuditEventType>(row.GetValue<string>("event_type")),
            Amount        = row.GetValue<decimal>("amount"),
            BalanceAfter  = row.GetValue<decimal>("balance_after"),
            ActorId       = row.GetValue<Guid>("actor_id"),
            IdempotencyKey = row.GetValue<string>("idempotency_key"),
            MetadataJson  = row.GetValue<string>("metadata")
        }).ToList();
    }
}
```

---

### 12.7 Register in DI

In `src/EWallet.Infrastructure/DependencyInjection.cs`:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ... existing registrations ...

    // ScyllaDB
    services.Configure<ScyllaDbOptions>(
        configuration.GetSection("ScyllaDb"));
    services.AddSingleton<ScyllaDbContext>();
    services.AddScoped<IAuditLogRepository, ScyllaAuditLogRepository>();

    return services;
}
```

`appsettings.json`:

```json
"ScyllaDb": {
  "ContactPoints": ["localhost"],
  "Port": 9042,
  "Username": "",
  "Password": ""
}
```

`appsettings.Production.json` (values from Secrets Manager / SSM):

```json
"ScyllaDb": {
  "ContactPoints": ["<scylladb-cloud-host>"],
  "Port": 9042,
  "Username": "<user>",
  "Password": "<password>"
}
```

---

### 12.8 Schema Bootstrap on Startup

Add a one-time initializer that runs before the app accepts traffic:

```csharp
// src/EWallet.Infrastructure/ScyllaDb/ScyllaSchemaInitializer.cs
public class ScyllaSchemaInitializer
{
    private readonly ISession _session;

    public ScyllaSchemaInitializer(ScyllaDbContext context)
        => _session = context.Session;

    public async Task InitializeAsync()
    {
        await _session.ExecuteAsync(new SimpleStatement(@"
            CREATE TABLE IF NOT EXISTS audit_log (
                wallet_id     UUID,
                occurred_at   TIMESTAMP,
                event_type    TEXT,
                amount        DECIMAL,
                balance_after DECIMAL,
                actor_id      UUID,
                idempotency_key TEXT,
                metadata      TEXT,
                PRIMARY KEY (wallet_id, occurred_at)
            ) WITH CLUSTERING ORDER BY (occurred_at DESC)
              AND default_time_to_live = 94608000
        "));

        // Add transaction_history table similarly...
    }
}
```

In `Program.cs`:

```csharp
using var scope = app.Services.CreateScope();
var initializer = scope.ServiceProvider.GetRequiredService<ScyllaSchemaInitializer>();
await initializer.InitializeAsync();
```

---

### 12.9 ScyllaDB Cloud (Managed, Free Trial)

[ScyllaDB Cloud](https://cloud.scylladb.com) offers a **30-day free trial** with a 3-node cluster. After that, the smallest cluster is ~$60/month — at that point, self-host on a single Fly.io machine (2 GB RAM) for ~$10/month, or use [Aiven for Apache Cassandra](https://aiven.io) (compatible, $19/month Startup plan).

For a **budget AWS deployment**, run ScyllaDB as a single Docker container on an EC2 `t3.medium` (2 vCPU / 4 GB RAM, ~$30/month) with an attached EBS `gp3` volume. This handles hundreds of thousands of AuditLog writes per day comfortably.

---

### 12.10 Data Model Rules for ScyllaDB

These rules prevent the most common Cassandra/ScyllaDB performance pitfalls:

- **Never use `SELECT *` without a partition key** — always filter by `wallet_id` or `user_id` first, or queries will full-scan every node.
- **Never UPDATE audit rows** — the audit log is immutable. If you need a correction, insert a new `StatusChange` event.
- **Set TTL on all tables** — audit logs don't need to live forever; 3 years (`94608000` seconds) is a sensible default that keeps partition sizes bounded.
- **Partition size target: < 100 MB** — if a single wallet generates millions of events, add `tx_date` as a partition key component (same pattern as `transaction_history` above).
- **Use `PreparedStatement` always** — never build CQL strings dynamically. Prepared statements are cached on the server and prevent CQL injection.
- **Consistency level `LocalQuorum`** — appropriate for single-datacenter deployments. Drop to `LocalOne` for maximum write throughput on the audit log if you can tolerate eventual consistency on reads.

---

*Last updated: May 2026 — OCN E-Wallet v1.0*