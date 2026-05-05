using EWallet.Domain.Abstractions;
using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Events;

public sealed record WalletCreditedEvent(Guid WalletId, Money Amount, string Description) : DomainEvent;
public sealed record WalletDebitedEvent(Guid WalletId, Money Amount, string Description) : DomainEvent;
public sealed record WalletLockedEvent(Guid WalletId) : DomainEvent;
public sealed record WalletUnlockedEvent(Guid WalletId) : DomainEvent;

public sealed record TransactionStatusChangedEvent(
    Guid TransactionId,
    TransactionStatus OldStatus,
    TransactionStatus NewStatus) : DomainEvent;

public sealed record UserKycUpgradedEvent(Guid UserId, KycLevel OldLevel, KycLevel NewLevel) : DomainEvent;
