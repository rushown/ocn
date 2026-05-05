using EWallet.Domain.Abstractions;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Entities;

public sealed class Wallet : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Money Balance { get; private set; } = default!;
    public bool IsLocked { get; private set; }

    private Wallet() { }

    public static Wallet Create(Guid userId)
    {
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = new Money(0m, "USD"),
            IsLocked = false
        };
        return wallet;
    }

    public void Credit(Money amount, string description)
    {
        if (IsLocked)
            throw new WalletLockedException();

        if (amount.IsZero)
            throw new DomainException("Credit amount must be greater than zero.");

        Balance = Balance.Add(amount);
        RaiseDomainEvent(new WalletCreditedEvent(Id, amount, description));
    }

    public void Debit(Money amount, string description)
    {
        if (IsLocked)
            throw new WalletLockedException();

        if (!Balance.IsGreaterThanOrEqualTo(amount))
            throw new InsufficientFundsException();

        Balance = Balance.Subtract(amount);
        RaiseDomainEvent(new WalletDebitedEvent(Id, amount, description));
    }

    public void Lock()
    {
        if (IsLocked)
            throw new DomainException("Wallet is already locked.");

        IsLocked = true;
        RaiseDomainEvent(new WalletLockedEvent(Id));
    }

    public void Unlock()
    {
        if (!IsLocked)
            throw new DomainException("Wallet is not locked.");

        IsLocked = false;
        RaiseDomainEvent(new WalletUnlockedEvent(Id));
    }
}
