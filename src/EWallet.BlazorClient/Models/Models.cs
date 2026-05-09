namespace EWallet.BlazorClient.Models;

// ─── Auth ─────────────────────────────────────────────────────────────────────

public record LoginRequest(string Email, string Password);

public record RegisterRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string Password,
    string ConfirmPassword);

public record AuthResponse(
    string AccessToken,
    UserDto User);

public record RefreshRequest();

// ─── User ─────────────────────────────────────────────────────────────────────

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    int KycLevel,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt);

public record UpdateProfileRequest(string FullName, string PhoneNumber);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

// ─── Wallet ───────────────────────────────────────────────────────────────────

public record WalletBalanceDto(
    Guid WalletId,
    decimal Balance,
    string Currency,
    DateTimeOffset LastUpdated);

public record DepositRequest(decimal Amount, string Currency, string? Description);

public record WithdrawRequest(decimal Amount, string Currency, string? Description);

public record TransferRequest(
    Guid ReceiverWalletId,
    decimal Amount,
    string Currency,
    string? Description,
    string? OtpCode);

// ─── Transactions ─────────────────────────────────────────────────────────────

public record TransactionDto(
    Guid Id,
    string Type,            // Deposit | Withdrawal | Transfer
    decimal Amount,
    string Currency,
    string Status,          // Pending | Completed | Failed
    string? Description,
    Guid? SenderWalletId,
    Guid? ReceiverWalletId,
    DateTimeOffset CreatedAt);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// ─── Recipient lookup ─────────────────────────────────────────────────────────

public record WalletLookupDto(
    Guid WalletId,
    string OwnerName,
    string Currency);

// ─── API error ────────────────────────────────────────────────────────────────

public record ApiError(string Message, IDictionary<string, string[]>? ValidationErrors = null);

// ─── OTP / 2FA ────────────────────────────────────────────────────────────────

public record Enable2FaResponse(string SharedKey, string AuthenticatorUri, string[] RecoveryCodes);
public record Verify2FaRequest(string Code);
