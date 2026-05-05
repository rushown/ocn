# OCN E-Wallet — Developer Runbook

## Prerequisites

- Docker Desktop (or Docker Engine + Compose)
- .NET 8 SDK
- Node.js 20+ (for any JS tooling)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

---

## First-Time Setup

```bash
# 1. Clone the repository
git clone <repo-url>
cd OCN

# 2. Start infrastructure
docker-compose up -d postgres redis

# 3. Verify containers
docker ps | grep -E "postgres|redis"

# 4. Apply EF Core migrations
dotnet ef database update \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API

# 5. Seed development data
docker-compose exec postgres \
  psql -U ewallet -d ewallet_db -f /seeds/init-db.sql
#   ⚠ Replace PLACEHOLDER_HASH values in init-db.sql first (see scripts/init-db.sql)

# 6. Run the API
dotnet run --project src/EWallet.API
# → Swagger: http://localhost:5000/swagger
# → Health:  http://localhost:5000/health

# 7. Run the Blazor frontend (separate terminal)
dotnet run --project src/EWallet.BlazorClient
# → http://localhost:5001
```

---

## Daily Development Workflow

```bash
# Start infra (if not already running)
docker-compose up -d postgres redis

# Run API + Blazor (separate terminals)
dotnet run --project src/EWallet.API
dotnet run --project src/EWallet.BlazorClient

# Tail structured logs
open http://localhost:8081          # Seq UI
```

---

## Running Tests

```bash
# Domain unit tests (no infra needed)
dotnet test tests/EWallet.Domain.Tests

# Application unit tests (no infra needed)
dotnet test tests/EWallet.Application.Tests

# Integration tests (requires Docker)
docker-compose up -d postgres redis
dotnet test tests/EWallet.Integration.Tests
```

---

## Database Operations

### Add a new EF Core migration
```bash
dotnet ef migrations add <MigrationName> \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API
```

### Apply pending migrations
```bash
dotnet ef database update \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API
```

### Reset dev database (⚠ destroys all data)
```bash
# Only works when DEPLOY_ENV != production
./scripts/reset-dev-db.sh

# Then re-run migrations and seed
dotnet ef database update \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API

docker-compose exec postgres \
  psql -U ewallet -d ewallet_db -f /seeds/init-db.sql
```

---

## Deployment

```bash
# Deploy (builds images, applies migrations)
DEPLOY_ENV=staging ./scripts/deploy.sh
```

The script:
1. `git pull origin main`
2. `docker-compose build --no-cache`
3. `docker-compose down && docker-compose up -d`
4. Waits for PostgreSQL
5. Runs `dotnet ef database update` inside the `api` container

---

## Service URLs (Development)

| Service | URL |
|---|---|
| API (Swagger) | http://localhost:5000/swagger |
| Blazor UI | http://localhost:5001 |
| Seq (logs) | http://localhost:8081 |
| Hangfire dashboard | http://localhost:5000/hangfire |
| Health check | http://localhost:5000/health |

---

## Development Credentials

| Account | Email | Password | KYC Tier |
|---|---|---|---|
| Admin | admin@ewallet.dev | Admin@1234! | Tier 3 |
| Test User | alice@example.com | Test@1234! | Tier 2 |

> These credentials exist only in the dev seed (`init-db.sql`). Never use in production.

---

## Troubleshooting

### Migrations fail with "relation does not exist"
The database was not created. Run:
```bash
docker-compose exec postgres psql -U ewallet -c "CREATE DATABASE ewallet_db;"
dotnet ef database update --project src/EWallet.Infrastructure --startup-project src/EWallet.API
```

### "Concurrency conflict" errors on transfers
Expected behavior — the optimistic concurrency check fired. The client should retry the request with a new idempotency key. Ensure `RowVersion` is included in your EF entity configuration.

### SignalR not receiving events
- Confirm the JWT is passed as `?access_token=<token>` on the WebSocket URL.
- Check CORS settings in `Program.cs` match the Blazor origin.
- Check Seq logs for `SignalR` category errors.

### Rate limit 429 errors
Wallet operation endpoints allow 10 req/min per user. Back off and retry after 60 seconds, or adjust `RateLimitOptions` in `appsettings.Development.json`.

### 2FA code rejected on transfer > $500
- Ensure device/authenticator clock is synced (TOTP is time-based).
- For dev testing, disable 2FA on the seed user: `UPDATE users SET is_two_factor_enabled = false WHERE email = 'alice@example.com';`

