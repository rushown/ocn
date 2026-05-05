# EWallet.BlazorClient

Standalone Blazor WebAssembly frontend for the EWallet platform, built with .NET 8, MudBlazor, Fluxor, and SignalR.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- EWallet API running on `http://localhost:5000`

---

## Getting Started

```bash
# Restore packages
dotnet restore

# Run development server
dotnet run

# Open browser
# → http://localhost:5001
```

---

## Project Structure

```
EWallet.BlazorClient/
├── App.razor                   # Root component, MudBlazor theme, router
├── _Imports.razor              # Global using statements
├── Program.cs                  # DI setup, services registration
│
├── Pages/
│   ├── Login.razor             # Auth page (AuthLayout)
│   ├── Register.razor          # Registration with password strength
│   ├── Dashboard.razor         # Main view: balance, quick actions, recent txs
│   ├── Transfer.razor          # Transfer page hosting TransferWizard
│   ├── Transactions.razor      # Paginated + filtered transaction history
│   └── Settings.razor          # Profile, password, 2FA, KYC
│
├── Components/
│   ├── BalanceCard.razor       # Gradient balance display card
│   ├── TransactionList.razor   # MudTable with sortable/colored rows
│   ├── OtpInput.razor          # 6-box OTP with auto-advance + timer
│   ├── TransferWizard.razor    # 4-step transfer wizard (self-contained)
│   ├── NavMenu.razor           # Sidebar nav + user info + logout
│   └── RedirectToLogin.razor   # Guard redirect component
│
├── Services/
│   ├── IServices.cs            # Service interfaces
│   ├── AuthService.cs          # Login, register, logout, JWT parsing
│   ├── WalletService.cs        # Balance, deposit, withdraw, transfer, txs
│   ├── SignalRService.cs       # Hub connection + balance/tx events
│   └── AuthorizationMessageHandler.cs  # Bearer token + silent refresh
│
├── State/                      # Fluxor state management
│   ├── WalletState.cs          # FeatureState record
│   ├── WalletActions.cs        # All action records
│   ├── WalletReducers.cs       # Pure reducer functions
│   └── WalletEffects.cs        # Side-effectful Fluxor effects
│
├── Models/
│   └── Models.cs               # DTOs, request/response records
│
├── Layout/
│   ├── MainLayout.razor        # Sidebar + AppBar layout
│   └── AuthLayout.razor        # Centered card for login/register
│
└── wwwroot/
    ├── index.html              # Host page with WASM loading spinner
    ├── appsettings.json        # API base URL config
    ├── css/app.css             # Custom styles + OTP/balance card overrides
    ├── js/app.js               # JS interop helpers (focus, clipboard)
    ├── service-worker.js       # Dev service worker
    └── service-worker.published.js  # Production PWA service worker
```

---

## Configuration

Edit `wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "http://localhost:5000"
}
```

---

## Docker

```bash
# Build
docker build -t ewallet-frontend .

# Run
docker run -p 8080:80 ewallet-frontend

# Open
# → http://localhost:8080
```

---

## Key Features

| Feature | Implementation |
|---|---|
| **State management** | Fluxor with Redux DevTools |
| **Optimistic UI** | Balance updated client-side before server confirms |
| **Real-time updates** | SignalR hub (`/hubs/wallet`) for balance + tx status |
| **Silent token refresh** | `AuthorizationMessageHandler` refreshes at <2 min expiry |
| **Idempotency keys** | Client-generated per operation, NOT regenerated on retry |
| **OTP input** | 6-box, auto-advance, paste support, 5-min countdown |
| **Accessibility** | `aria-label` on all interactive elements |
| **Responsive** | MudBlazor breakpoints (xs/sm/md) |
| **PWA** | Service worker with cache-first strategy |

---

## Design Tokens (MudBlazor Theme)

| Token | Light | Dark |
|---|---|---|
| Primary | `#2563EB` | `#3B82F6` |
| Secondary | `#7C3AED` | `#8B5CF6` |
| AppBar | `#1E3A8A` | `#0F172A` |
| Success | `#16A34A` | `#22C55E` |
| Error | `#DC2626` | `#EF4444` |
| Font | Inter, Roboto, sans-serif | — |
