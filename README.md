<div align="center">

```
  ██████╗  ██████╗███╗   ██╗
 ██╔═══██╗██╔════╝████╗  ██║
 ██║   ██║██║     ██╔██╗ ██║
 ██║   ██║██║     ██║╚██╗██║
 ╚██████╔╝╚██████╗██║ ╚████║
  ╚═════╝  ╚═════╝╚═╝  ╚═══╝
```

**Open Currency Network** — A production-grade digital wallet platform built on .NET 8

[![Build](https://img.shields.io/github/actions/workflow/status/rushown/ocn/ci.yml?branch=main&style=flat-square&color=0d9e75&label=build)](https://github.com/rushown/ocn/actions)
[![License](https://img.shields.io/badge/license-MIT-185fa5?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-534ab7?style=flat-square)](https://dotnet.microsoft.com)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-3c3489?style=flat-square)](https://www.postgresql.org)
[![Docker](https://img.shields.io/badge/docker-ready-0f6e56?style=flat-square)](docker-compose.yml)

</div>

---

## What is OCN?

OCN is a high-performance, event-driven e-wallet API and dashboard. It handles real-time balance management, multi-currency transactions, background job scheduling, and live notifications — all running behind a clean Blazor WebAssembly front-end.

> Built with Clean Architecture principles, CQRS via MediatR, and a full observability stack out of the box.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        OCN System                               │
│                                                                 │
│  ┌──────────────┐     ┌──────────────┐     ┌────────────────┐  │
│  │   Blazor     │────▶│  ASP.NET 8   │────▶│  PostgreSQL 15 │  │
│  │    WASM      │◀────│   REST API   │     └────────────────┘  │
│  └──────────────┘     │  + SignalR   │                         │
│                       │  + Hangfire  │────▶┌────────────────┐  │
│                       └──────────────┘     │    Redis 7     │  │
│                             │              └────────────────┘  │
│                       ┌─────▼──────┐                           │
│                       │  MediatR   │                           │
│                       │  Pipeline  │                           │
│                       │  Commands  │                           │
│                       │  Queries   │                           │
│                       └────────────┘                           │
└─────────────────────────────────────────────────────────────────┘
```

### Project Layout

```
ocn/
├── src/
│   ├── EWallet.API/              # ASP.NET Core 8 — controllers, middleware, SignalR hubs
│   ├── EWallet.Application/      # MediatR commands, queries, validators, DTOs
│   ├── EWallet.Domain/           # Entities, aggregates, domain events, value objects
│   ├── EWallet.Infrastructure/   # EF Core, Redis, Hangfire, email, external services
│   └── EWallet.BlazorClient/     # Blazor WASM standalone frontend
├── tests/
│   ├── EWallet.UnitTests/        # xUnit + Moq — domain & application layer
│   ├── EWallet.IntegrationTests/ # Testcontainers — real PostgreSQL & Redis
│   └── EWallet.E2ETests/         # End-to-end scenarios
├── scripts/                      # DB seed, migration helpers, dev utilities
├── docs/                         # Architecture decisions, API reference
├── docker-compose.yml
├── Dockerfile
└── EWalletApp.sln
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 / ASP.NET Core 8 |
| Frontend | Blazor WebAssembly (standalone) |
| Database | PostgreSQL 15 + EF Core 8 |
| Cache | Redis 7 |
| Messaging | MediatR 12 (CQRS) |
| Background Jobs | Hangfire |
| Real-time | SignalR |
| Auth | JWT + Refresh Token rotation |
| Validation | FluentValidation |
| Logging | Serilog (structured, JSON) |
| Testing | xUnit + Moq + Testcontainers |

---

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [Node.js 18+](https://nodejs.org/) *(for Blazor tooling)*

### 1 — Spin up infrastructure

```bash
git clone https://github.com/rushown/ocn.git
cd ocn
docker-compose up -d postgres redis
```

This starts PostgreSQL 15 on `:15432` and Redis 7 on `:6379`.

### 2 — Configure secrets

```bash
cp src/EWallet.API/appsettings.Development.example.json \
   src/EWallet.API/appsettings.Development.json
```

Edit `appsettings.Development.json` and fill in:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=15432;Database=ewallet_db;Username=ewallet;Password=ewallet_secret",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Secret": "CHANGE_ME_IN_PRODUCTION_USE_ENV_VAR_256_BITS_MINIMUM",
    "Issuer": "ewallet-api",
    "Audience": "ewallet-client",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

### 3 — Run migrations

```bash
dotnet ef database update \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API
```

### 4 — Start the API

```bash
dotnet run --project src/EWallet.API
# API:       http://localhost:5002
# Swagger:   http://localhost:5002/swagger
# Hangfire:  http://localhost:5002/hangfire
# SignalR:   http://localhost:5002/hubs/wallet
```

### 5 — Start the Blazor client

```bash
dotnet run --project src/EWallet.BlazorClient
# Client:    http://localhost:5001
```

### 6 — Quick auth smoke test

If you run auth curl commands with a password containing `!`, disable bash history expansion first:

```bash
set +H
```

Then run:

```bash
TS=$(date +%s)
EMAIL="test${TS}@example.com"
PHONE="+1555${TS: -7}"

curl -sS -i -X POST http://localhost:5002/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"phoneNumber\":\"$PHONE\",\"fullName\":\"Test User\",\"password\":\"Test@1234!\",\"confirmPassword\":\"Test@1234!\"}"

curl -sS -i -X POST http://localhost:5002/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"Test@1234!\"}"
```

---

## Docker (Production)

Build and run the full stack with a single command:

```bash
docker build -t ocn:latest .
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

The production compose file sets `ASPNETCORE_ENVIRONMENT=Production`, enables HTTPS redirection, and mounts persistent volumes for Postgres data.

---

## API Reference

Full OpenAPI spec at `/swagger` when running. Key endpoints:

```
POST   /api/auth/register          Register a new user
POST   /api/auth/login             Obtain JWT + refresh token
POST   /api/auth/refresh           Rotate refresh token

GET    /api/wallets                List user wallets
POST   /api/wallets                Create wallet
GET    /api/wallets/{id}/balance   Get balance (cached in Redis)

POST   /api/transactions/deposit   Deposit funds
POST   /api/transactions/withdraw  Withdraw funds
POST   /api/transactions/transfer  Transfer between wallets
GET    /api/transactions           Paginated transaction history

GET    /api/notifications          User notifications
WS     /hubs/wallet                SignalR real-time events
```

---

## Authentication Flow

OCN uses short-lived JWTs (15 min) with rotating refresh tokens (7 days):

```
Client                          API
  │                              │
  ├── POST /auth/login ─────────▶│
  │◀─────────────── JWT + RT ────┤
  │                              │
  ├── [any request + JWT] ──────▶│
  │◀─────────────── 200 ─────────┤
  │                              │
  ├── [JWT expires]              │
  ├── POST /auth/refresh ───────▶│
  │◀─────────────── new JWT + RT─┤
```

Refresh tokens are stored hashed in Postgres and invalidated on use (rotation). All token families are revocable server-side.

---

## Running Tests

```bash
# Unit tests (fast, no Docker required)
dotnet test tests/EWallet.UnitTests

# Integration tests (Testcontainers spins up real Postgres + Redis)
dotnet test tests/EWallet.IntegrationTests

# All tests with coverage
dotnet test --collect:"XPlat Code Coverage" \
            --results-directory coverage/
```

---

## Background Jobs

Hangfire is wired with a PostgreSQL storage backend. Registered jobs:

| Job | Schedule | Description |
|---|---|---|
| `ExpireRefreshTokensJob` | Every 6 hours | Purge expired refresh tokens |
| `GenerateMonthlyStatements` | 1st of month | Build PDF statements |
| `SendTransactionDigest` | Daily 08:00 | Email activity summaries |
| `ReconcileBalancesJob` | Hourly | Verify Redis vs DB consistency |

Dashboard available at `/hangfire` (requires `Admin` role in production).

---

## Observability

Serilog is configured to write structured JSON logs to console and (optionally) to a file sink or a remote provider. Log context is enriched with:

- `RequestId`, `UserId`, `WalletId` via middleware
- `MachineName`, `ThreadId`, `Assembly` via Serilog enrichers
- Correlation IDs propagated through MediatR pipeline behaviours

To ship logs to Seq, Datadog, or ELK, add the corresponding Serilog sink package and configure the sink in `appsettings.json`.

---

## Configuration Reference

| Key | Default | Description |
|---|---|---|
| `Jwt:ExpiryMinutes` | `15` | JWT access token lifetime |
| `Jwt:RefreshExpiryDays` | `7` | Refresh token lifetime |
| `Redis:BalanceTtlSeconds` | `300` | Balance cache TTL |
| `Hangfire:WorkerCount` | `5` | Background job concurrency |
| `RateLimiting:PerMinute` | `60` | Requests per user per minute |
| `Cors:AllowedOrigins` | `localhost` | Allowed CORS origins |

---

## Contributing

1. Fork the repo and create a feature branch: `git checkout -b feat/my-feature`
2. Follow the existing architecture patterns — new features belong in Application layer commands/queries
3. Add unit tests for domain logic, integration tests for infrastructure
4. Run `dotnet test` and confirm all tests pass
5. Open a pull request against `main`

Please keep PRs focused. One feature or fix per PR.

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

<div align="center">

Built by [rushown](https://github.com/rushown)

</div>