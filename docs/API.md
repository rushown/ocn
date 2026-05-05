# OCN E-Wallet — API Reference

Base URL (dev): `http://localhost:5000`
Interactive docs: `http://localhost:5000/swagger`

All wallet operation endpoints are rate-limited to **10 requests/min per user**.
All amounts are `decimal` strings (e.g., `"250.00"`).
All IDs in responses are opaque `Guid` strings — never internal integer IDs.

---

## Authentication

### POST /api/auth/register
Register a new user account.

**Request body:**
```json
{
  "email": "user@example.com",
  "phoneNumber": "+15551234567",
  "fullName": "Jane Doe",
  "password": "Strong@Pass1!"
}
```

**Response `201 Created`:**
```json
{
  "userId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "email": "user@example.com"
}
```

---

### POST /api/auth/login
Authenticate and receive tokens.

**Request body:**
```json
{
  "email": "user@example.com",
  "password": "Strong@Pass1!",
  "twoFactorCode": "123456"   // required if 2FA is enabled
}
```

**Response `200 OK`:**
```json
{
  "accessToken": "<JWT>",
  "expiresIn": 3600
}
```
Refresh token is set as `HttpOnly` cookie (`refreshToken`).

---

### POST /api/auth/refresh
Exchange a refresh token for a new access token.
Reads the `refreshToken` cookie automatically.

**Response `200 OK`:** same shape as `/login`.

---

### POST /api/auth/logout
Revokes the current refresh token.

---

## Wallets

All wallet endpoints require `Authorization: Bearer <accessToken>`.

### GET /api/wallets/me
Returns the authenticated user's wallet.

**Response `200 OK`:**
```json
{
  "walletId": "...",
  "balanceAmount": "5000.00",
  "balanceCurrency": "USD",
  "isLocked": false
}
```

---

### POST /api/wallets/transfer
Transfer funds to another wallet.
**Requires header:** `Idempotency-Key: wallet_transfer_{userId}_{timestamp}_{nonce}`
**2FA code required** if `amount > 500.00`.

**Request body:**
```json
{
  "recipientWalletId": "...",
  "amount": "250.00",
  "currency": "USD",
  "twoFactorCode": "123456",
  "idempotencyKey": "wallet_transfer_abc_1700000000_xyz"
}
```

**Response `202 Accepted`:**
```json
{
  "transactionId": "...",
  "status": "Pending"
}
```
Final status is pushed over SignalR (`/hubs/wallet`, event `TransactionStatusUpdated`).

---

### GET /api/wallets/transactions
Paginated transaction history.

**Query params:** `page` (default 1), `pageSize` (default 20, max 100)

**Response `200 OK`:**
```json
{
  "items": [
    {
      "transactionId": "...",
      "type": "Debit",
      "amount": "250.00",
      "currency": "USD",
      "status": "Completed",
      "createdAt": "2024-01-01T12:00:00Z"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20
}
```

---

## Health Check

### GET /health
No authentication required.

**Response `200 OK`:**
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "hangfire": "Healthy"
  }
}
```

---

## Error Responses

All errors follow the RFC 7807 `ProblemDetails` format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Error",
  "status": 400,
  "detail": "Amount must be greater than zero.",
  "traceId": "00-abc123..."
}
```

| Status | Meaning |
|---|---|
| 400 | Validation error / bad request |
| 401 | Missing or invalid JWT |
| 403 | Forbidden (e.g., insufficient KYC tier) |
| 409 | Idempotency key conflict |
| 422 | Business rule violation (e.g., daily limit exceeded) |
| 429 | Rate limit exceeded |
| 500 | Internal server error |

---

## SignalR Hub

Endpoint: `ws://localhost:5000/hubs/wallet`
Authentication: pass JWT as query param `?access_token=<JWT>`.

| Event | Payload |
|---|---|
| `TransactionStatusUpdated` | `{ transactionId, status, updatedAt }` |
| `BalanceUpdated` | `{ walletId, newBalance }` |

