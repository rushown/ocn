namespace EWallet.API.Models;

// ─── Auth ──────────────────────────────────────────────────────────────────────

public record RegisterRequest(
    string Email,
    string PhoneNumber,
    string FullName,
    string Password,
    string ConfirmPassword);

public record LoginRequest(
    string Email,
    string Password);

// Refresh/logout tokens are handled via HttpOnly cookies.
public record RefreshTokenRequest();
public record LogoutRequest();

// ─── Wallet ───────────────────────────────────────────────────────────────────

public record DepositRequest(
    decimal Amount,
    string Currency = "USD",
    string ExternalRef = "");

public record WithdrawRequest(
    decimal Amount,
    string Currency = "USD",
    string ExternalRef = "");

public record TransferRequest(
    Guid RecipientWalletId,
    decimal Amount,
    string Currency = "USD",
    string? Description = null,
    string? OtpCode = null);

// ─── User ─────────────────────────────────────────────────────────────────────

public record UpdateProfileRequest(
    string FullName,
    string PhoneNumber);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

public record VerifyOtpRequest(
    string OtpCode,
    string Purpose);    // e.g. "2FA_SETUP" | "HIGH_VALUE_TRANSFER"

// ─── Admin ────────────────────────────────────────────────────────────────────

public record LockWalletRequest(
    string Reason);
