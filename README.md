# EWallet Production App — Scaffold

## Quick Start

```bash
# 1. Clone / unzip this scaffold
# 2. Start dependencies
docker-compose up -d

# 3. Follow _PROMPT.md in root — give each sub-prompt to a separate Claude session
# 4. After all sessions complete:
dotnet build EWalletApp.sln
dotnet ef database update --project src/EWallet.Infrastructure --startup-project src/EWallet.API
dotnet run --project src/EWallet.API
```

## Scaffold Map
Every folder contains a `_PROMPT.md` — paste it into Claude to generate that layer's code.
See root `_PROMPT.md` for session order and dependency rules.

## Tech Stack
- .NET 8 / ASP.NET Core 8
- Blazor WebAssembly (standalone)
- PostgreSQL 15 + EF Core 8
- Redis 7
- Hangfire
- SignalR
- JWT + Refresh Tokens
- Serilog
- FluentValidation
- MediatR 12
- xUnit + Moq + Testcontainers
