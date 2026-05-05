# EWallet.Infrastructure — Database Migrations

## Prerequisites
- .NET 8 SDK
- PostgreSQL running locally or via Docker
- `dotnet ef` CLI tool: `dotnet tool install --global dotnet-ef`

## Connection String
Set your connection string in `src/EWallet.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ewallet_dev;Username=postgres;Password=yourpassword",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "Secret": "your-secret-key-minimum-32-characters-long",
    "Issuer": "EWallet",
    "Audience": "EWallet.Client"
  }
}
```

## Create Initial Migration

```bash
dotnet ef migrations add InitialCreate \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API \
  --output-dir Persistence/Migrations
```

## Apply Migration to Database

```bash
dotnet ef database update \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API
```

## Rollback (if needed)

```bash
# Roll back to the previous migration
dotnet ef database update PreviousMigrationName \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API

# Remove the last unapplied migration from the project
dotnet ef migrations remove \
  --project src/EWallet.Infrastructure \
  --startup-project src/EWallet.API
```

## Register Hangfire Recurring Jobs
Call this once after app.Build() in Program.cs:

```csharp
using EWallet.Infrastructure.BackgroundJobs;

// After app.UseHangfireDashboard() / app.UseHangfireServer()
HangfireSetup.RegisterRecurringJobs();
```

## Tables Created by InitialCreate

| Table           | Notes                                         |
|-----------------|-----------------------------------------------|
| users           | Soft-delete filter (IsActive = true)          |
| wallets         | One per user; balance stored as decimal(18,2) |
| transactions    | Idempotency key unique index                  |
| audit_logs      | Insert-only; no FK constraints                |
| otp_records     | Cascade-deleted with user                     |
| hangfire.*      | Hangfire schema (separate from domain tables) |
