# OCN E-Wallet — Architecture Document

## Technology Stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core 8 Web API (REST + SignalR) |
| Frontend | Blazor WebAssembly .NET 8 (standalone) |
| Database | PostgreSQL + EF Core 8 |
| Cache | Redis (StackExchange.Redis) |
| Queue | Hangfire background jobs |
| Auth | JWT + Refresh Token rotation (7-day expiry) |
| Payments | Simulated FakePaymentGateway |
| Logging | Serilog + Seq sink |

---

## Solution Structure

```
OCN/
├── src/
│   ├── EWallet.Domain/            # Entities, VOs, Domain Events, Enums, Interfaces
│   ├── EWallet.Application/       # CQRS, MediatR handlers, DTOs, Validators, Mappers
│   ├── EWallet.Infrastructure/    # EF Core, Repositories, Redis, Hangfire, Gateway
│   ├── EWallet.API/               # Controllers, SignalR Hub, Middleware, Program.cs
│   └── EWallet.BlazorClient/      # Pages, Components, HttpClient services, Fluxor
├── tests/
│   ├── EWallet.Domain.Tests/
│   ├── EWallet.Application.Tests/
│   └── EWallet.Integration.Tests/
├── scripts/
│   ├── init-db.sql
│   ├── deploy.sh
│   └── reset-dev-db.sh
└── docs/
    ├── ARCHITECTURE.md   ← this file
    ├── API.md
    └── RUNBOOK.md
```

---

## Layer Dependency Rules

```
BlazorClient  ──►  API  ──►  Application  ──►  Domain
                    │                │
                    └──►  Infrastructure ──────►  Domain
                                     └──────────►  Application (interfaces)
```

- **Domain** has zero external dependencies.
- **Application** depends only on Domain (interfaces, not Infrastructure).
- **Infrastructure** implements Application interfaces and references EF Core, Redis, etc.
- **API** wires everything via DI in `Program.cs`.
- **BlazorClient** communicates with API over HTTP/SignalR only.

---

## Architecture Rules

1. **Money is always `decimal` (2 d.p.).** Never use `float` or `double` for currency.
2. **All balance mutations go through domain methods** — no direct property assignment outside the entity.
3. **Every transaction carries an idempotency key** — format: `wallet_transfer_{userId}_{timestamp}_{nonce}`.
4. **Unit of Work** wraps all multi-repository operations in a single DB transaction.
5. **Optimistic concurrency** (`byte[] RowVersion`) on `Wallet` and `Transaction` entities.
6. **Pessimistic locking** (`SELECT FOR UPDATE`) for transfers exceeding $1,000.
7. **2FA required** for any single transfer above $500.
8. **AuditLog** entry written for every Debit, Credit, and status change.
9. **Daily limits:** Tier 1 = $1,000 | Tier 2 = $5,000 | Tier 3 = unlimited.
10. **Never expose internal IDs** in API responses — use opaque external IDs (`Guid`).

---

## Cross-Cutting Concerns

| Concern | Implementation |
|---|---|
| Logging | Serilog + structured logs + Seq sink (`http://localhost:8081`) |
| Validation | FluentValidation pipeline behavior (MediatR) |
| Error handling | `Result<T>` pattern — never throw across layer boundaries |
| Auth | JWT Bearer + Refresh token (HttpOnly cookie) |
| Rate limiting | Fixed window: 10 req/min per user on wallet operations |
| Health checks | `/health` endpoint — checks DB, Redis, and Hangfire |
| CORS | Configured for Blazor WASM origin only |

---

## Data Flow — Wallet Transfer

```
BlazorClient
  │  POST /api/wallets/transfer  (Idempotency-Key header)
  ▼
API Controller
  │  Validate JWT → extract userId
  │  Check 2FA flag if amount > $500
  ▼
MediatR → TransferCommand Handler (Application layer)
  │  FluentValidation pipeline
  │  Check daily limit (KYC tier)
  │  Resolve idempotency key (Infrastructure store)
  ▼
Unit of Work (Infrastructure)
  │  SELECT FOR UPDATE if amount > $1,000
  │  Wallet.Debit(amount)   — domain method
  │  Wallet.Credit(amount)  — domain method
  │  Transaction entity created
  │  AuditLog entry written
  │  SaveChanges (optimistic concurrency check)
  ▼
Hangfire Job (async)
  │  FakePaymentGateway.ProcessAsync()
  │  Transaction status updated → SignalR push to client
  ▼
BlazorClient receives real-time status update
```

---

## KYC Tiers

| Tier | Daily Limit | 2FA on Transfer >$500 | Notes |
|---|---|---|---|
| Tier 1 | $1,000 | Required | Default for new users |
| Tier 2 | $5,000 | Required | ID-verified |
| Tier 3 | Unlimited | Required | Fully KYC-verified |

