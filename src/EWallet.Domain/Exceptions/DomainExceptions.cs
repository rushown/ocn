namespace EWallet.Domain.Exceptions;

/// <summary>Base exception for all domain-layer violations.</summary>
public class DomainException : Exception
{
    /// <summary>Optional machine-readable error code.</summary>
    public string? ErrorCode { get; }

    /// <summary>Initializes a new <see cref="DomainException"/> with a message.</summary>
    public DomainException(string message, string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>Thrown when a debit would reduce a wallet balance below zero.</summary>
public class InsufficientFundsException : DomainException
{
    /// <summary>Initializes a new <see cref="InsufficientFundsException"/>.</summary>
    public InsufficientFundsException()
        : base("Insufficient funds in wallet", "INSUFFICIENT_FUNDS") { }
}

/// <summary>Thrown when an operation is attempted on a locked wallet.</summary>
public class WalletLockedException : DomainException
{
    /// <summary>Initializes a new <see cref="WalletLockedException"/>.</summary>
    public WalletLockedException()
        : base("Wallet is currently locked", "WALLET_LOCKED") { }
}

/// <summary>Thrown when a transaction status transition is not permitted.</summary>
public class InvalidTransactionStateException : DomainException
{
    /// <summary>The status the transaction was in before the attempted transition.</summary>
    public string FromStatus { get; }

    /// <summary>The status that was illegally attempted.</summary>
    public string ToStatus { get; }

    /// <summary>
    /// Initializes a new <see cref="InvalidTransactionStateException"/> describing the illegal transition.
    /// </summary>
    public InvalidTransactionStateException(string fromStatus, string toStatus)
        : base($"Cannot transition transaction from '{fromStatus}' to '{toStatus}'", "INVALID_TRANSACTION_STATE")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}

/// <summary>Thrown when a transaction would exceed the wallet owner's daily debit limit.</summary>
public class DailyLimitExceededException : DomainException
{
    /// <summary>The configured daily limit that would be exceeded.</summary>
    public decimal Limit { get; }

    /// <summary>The amount that was attempted.</summary>
    public decimal AttemptedAmount { get; }

    /// <summary>
    /// Initializes a new <see cref="DailyLimitExceededException"/>.
    /// </summary>
    public DailyLimitExceededException(decimal limit, decimal attemptedAmount)
        : base($"Daily debit limit of {limit:F2} would be exceeded by amount {attemptedAmount:F2}", "DAILY_LIMIT_EXCEEDED")
    {
        Limit = limit;
        AttemptedAmount = attemptedAmount;
    }
}

/// <summary>Thrown when an OTP code has passed its expiry time.</summary>
public class OtpExpiredException : DomainException
{
    /// <summary>Initializes a new <see cref="OtpExpiredException"/>.</summary>
    public OtpExpiredException()
        : base("The OTP code has expired", "OTP_EXPIRED") { }
}

/// <summary>Thrown when an OTP code has already been consumed.</summary>
public class OtpAlreadyUsedException : DomainException
{
    /// <summary>Initializes a new <see cref="OtpAlreadyUsedException"/>.</summary>
    public OtpAlreadyUsedException()
        : base("The OTP code has already been used", "OTP_ALREADY_USED") { }
}
