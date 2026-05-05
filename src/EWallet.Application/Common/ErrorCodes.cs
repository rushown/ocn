namespace EWallet.Application.Common;

public static class ErrorCodes
{
    public const string InsufficientFunds         = "INSUFFICIENT_FUNDS";
    public const string DailyLimitExceeded        = "DAILY_LIMIT_EXCEEDED";
    public const string WalletLocked              = "WALLET_LOCKED";
    public const string InvalidOtp                = "INVALID_OTP";
    public const string OtpExpired                = "OTP_EXPIRED";
    public const string DuplicateTransaction      = "DUPLICATE_TRANSACTION";
    public const string UserNotFound              = "USER_NOT_FOUND";
    public const string WalletNotFound            = "WALLET_NOT_FOUND";
    public const string UnauthorizedWalletAccess  = "UNAUTHORIZED_WALLET_ACCESS";
    public const string ConcurrencyConflict       = "CONCURRENCY_CONFLICT";
}
