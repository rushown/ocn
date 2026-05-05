namespace EWallet.API.Models;

// ─── Auth ──────────────────────────────────────────────────────────────────────

public record RegisterRequest(
    string Email,
    string PhoneNumber,
    string FullName,
    string Password);

public record LoginRequest(
    string Email,
    string Password);

public record RefreshTokenRequest(
    string RefreshToken);

public record LogoutRequest(
    string RefreshToken);

// ─── Wallet ───────────────────────────────────────────────────────────────────

public record DepositRequest(
    decimal Amount,
    string Currency = "USD");

public record WithdrawRequest(
    decimal Amount,
    string Currency = "USD");

public record TransferRequest(
    Guid RecipientWalletId,
    decimal Amount,
    string Currency = "USD",
    string? OtpCode = null);

// ─── User ─────────────────────────────────────────────────────────────────────

public record UpdateProfileRequest(
    string FullName,
    string PhoneNumber);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record VerifyOtpRequest(
    string OtpCode,
    string Purpose);    // e.g. "2FA_SETUP" | "HIGH_VALUE_TRANSFER"

// ─── Admin ────────────────────────────────────────────────────────────────────

public record LockWalletRequest(
    string Reason);
