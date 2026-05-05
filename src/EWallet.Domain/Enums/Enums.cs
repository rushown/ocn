namespace EWallet.Domain.Enums;

/// <summary>Lifecycle status of a wallet transaction.</summary>
public enum TransactionStatus
{
    /// <summary>Transaction has been created but not yet processed.</summary>
    Pending = 0,

    /// <summary>Transaction was processed successfully.</summary>
    Completed = 1,

    /// <summary>Transaction processing failed.</summary>
    Failed = 2,

    /// <summary>Transaction was refunded after completion.</summary>
    Refunded = 3,

    /// <summary>Transaction was reversed after completion.</summary>
    Reversed = 4
}

/// <summary>Category of a wallet transaction.</summary>
public enum TransactionType
{
    /// <summary>Funds added to a wallet from an external source.</summary>
    Deposit = 0,

    /// <summary>Funds removed from a wallet to an external destination.</summary>
    Withdrawal = 1,

    /// <summary>Funds moved between two wallets.</summary>
    Transfer = 2,

    /// <summary>A refund of a previous transaction.</summary>
    Refund = 3
}

/// <summary>KYC verification tier for a user, controlling daily transaction limits.</summary>
public enum KycLevel
{
    /// <summary>No KYC completed — $0 daily limit.</summary>
    Unverified = 0,

    /// <summary>Basic KYC — $1,000 daily limit.</summary>
    Tier1 = 1,

    /// <summary>Enhanced KYC — $5,000 daily limit.</summary>
    Tier2 = 2,

    /// <summary>Full KYC — unlimited daily transactions.</summary>
    Tier3 = 3
}

/// <summary>The purpose for which an OTP code was generated.</summary>
public enum OtpPurpose
{
    /// <summary>OTP to authorise a wallet transfer.</summary>
    Transfer = 0,

    /// <summary>OTP to verify a login attempt.</summary>
    Login = 1,

    /// <summary>OTP to verify a phone number.</summary>
    PhoneVerification = 2,

    /// <summary>OTP to authorise a password reset.</summary>
    PasswordReset = 3
}
