using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Events;

/// <summary>Raised when a new transaction is created.</summary>
public record TransactionCreatedEvent : BaseEvent
{
    /// <summary>The ID of the newly created transaction.</summary>
    public Guid TransactionId { get; init; }

    /// <summary>The wallet the transaction belongs to.</summary>
    public Guid WalletId { get; init; }

    /// <summary>The monetary amount of the transaction.</summary>
    public Money Amount { get; init; }

    /// <summary>The type of transaction.</summary>
    public TransactionType Type { get; init; }

    /// <summary>Initializes a new <see cref="TransactionCreatedEvent"/>.</summary>
    public TransactionCreatedEvent(Guid transactionId, Guid walletId, Money amount, TransactionType type)
    {
        TransactionId = transactionId;
        WalletId = walletId;
        Amount = amount;
        Type = type;
    }
}

/// <summary>Raised when a transaction's status transitions to a new state.</summary>
public record TransactionStatusChangedEvent : BaseEvent
{
    /// <summary>The ID of the transaction whose status changed.</summary>
    public Guid TransactionId { get; init; }

    /// <summary>The status before the transition.</summary>
    public TransactionStatus OldStatus { get; init; }

    /// <summary>The status after the transition.</summary>
    public TransactionStatus NewStatus { get; init; }

    /// <summary>Initializes a new <see cref="TransactionStatusChangedEvent"/>.</summary>
    public TransactionStatusChangedEvent(Guid transactionId, TransactionStatus oldStatus, TransactionStatus newStatus)
    {
        TransactionId = transactionId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}

/// <summary>Raised when a wallet receives a credit.</summary>
public record WalletCreditedEvent : BaseEvent
{
    /// <summary>The wallet that was credited.</summary>
    public Guid WalletId { get; init; }

    /// <summary>The amount credited.</summary>
    public Money Amount { get; init; }

    /// <summary>The wallet's balance after the credit.</summary>
    public Money NewBalance { get; init; }

    /// <summary>Human-readable description of the credit.</summary>
    public string Description { get; init; }

    /// <summary>Initializes a new <see cref="WalletCreditedEvent"/>.</summary>
    public WalletCreditedEvent(Guid walletId, Money amount, Money newBalance, string description)
    {
        WalletId = walletId;
        Amount = amount;
        NewBalance = newBalance;
        Description = description;
    }
}

/// <summary>Raised when a wallet is debited.</summary>
public record WalletDebitedEvent : BaseEvent
{
    /// <summary>The wallet that was debited.</summary>
    public Guid WalletId { get; init; }

    /// <summary>The amount debited.</summary>
    public Money Amount { get; init; }

    /// <summary>The wallet's balance after the debit.</summary>
    public Money NewBalance { get; init; }

    /// <summary>Human-readable description of the debit.</summary>
    public string Description { get; init; }

    /// <summary>Initializes a new <see cref="WalletDebitedEvent"/>.</summary>
    public WalletDebitedEvent(Guid walletId, Money amount, Money newBalance, string description)
    {
        WalletId = walletId;
        Amount = amount;
        NewBalance = newBalance;
        Description = description;
    }
}

/// <summary>Raised when a wallet's locked state changes.</summary>
public record WalletLockedEvent : BaseEvent
{
    /// <summary>The wallet that was locked or unlocked.</summary>
    public Guid WalletId { get; init; }

    /// <summary>The reason for locking (null when unlocking).</summary>
    public string? Reason { get; init; }

    /// <summary>Initializes a new <see cref="WalletLockedEvent"/>.</summary>
    public WalletLockedEvent(Guid walletId, string? reason)
    {
        WalletId = walletId;
        Reason = reason;
    }
}

/// <summary>Raised when a user's KYC level is upgraded.</summary>
public record UserKycUpgradedEvent : BaseEvent
{
    /// <summary>The user whose KYC level changed.</summary>
    public Guid UserId { get; init; }

    /// <summary>The KYC level before the upgrade.</summary>
    public KycLevel OldLevel { get; init; }

    /// <summary>The KYC level after the upgrade.</summary>
    public KycLevel NewLevel { get; init; }

    /// <summary>Initializes a new <see cref="UserKycUpgradedEvent"/>.</summary>
    public UserKycUpgradedEvent(Guid userId, KycLevel oldLevel, KycLevel newLevel)
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
    }
}
