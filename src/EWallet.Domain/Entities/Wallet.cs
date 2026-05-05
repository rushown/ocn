using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Entities;

/// <summary>
/// Represents a user's e-wallet holding a monetary balance.
/// Supports credit, debit, and lock/unlock operations with domain-event emission.
/// Optimistic concurrency is managed via <see cref="RowVersion"/>.
/// </summary>
public sealed class Wallet : BaseEntity
{
    /// <summary>The ID of the user who owns this wallet.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Current wallet balance.</summary>
    public Money Balance { get; private set; } = default!;

    /// <summary>ISO-4217 currency code for this wallet (e.g. "USD").</summary>
    public string Currency { get; private set; } = default!;

    /// <summary>
    /// EF Core concurrency token. Set by the database; do not assign manually.
    /// </summary>
    public byte[]? RowVersion { get; private set; }

    /// <summary>Whether the wallet is currently locked and cannot be debited or credited.</summary>
    public bool IsLocked { get; private set; }

    // EF Core parameterless constructor
    private Wallet() { }

    /// <summary>
    /// Creates a new wallet for a given user.
    /// </summary>
    /// <param name="userId">The owning user's ID.</param>
    /// <param name="currency">ISO-4217 currency code. Defaults to "USD".</param>
    /// <returns>A new <see cref="Wallet"/> with a zero balance.</returns>
    public static Wallet Create(Guid userId, string currency = "USD")
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId cannot be empty");

        var wallet = new Wallet
        {
            UserId = userId,
            Currency = currency.ToUpperInvariant(),
            Balance = Money.Zero(currency)
        };
        return wallet;
    }

    /// <summary>
    /// Credits the wallet by <paramref name="amount"/>.
    /// Raises <see cref="WalletCreditedEvent"/>.
    /// </summary>
    /// <param name="amount">Positive monetary amount in the same currency as the wallet.</param>
    /// <param name="description">Human-readable description of the credit reason.</param>
    /// <exception cref="WalletLockedException">Thrown when the wallet is locked.</exception>
    /// <exception cref="DomainException">Thrown when <paramref name="amount"/> is zero or negative.</exception>
    public void Credit(Money amount, string description)
    {
        if (IsLocked)
            throw new WalletLockedException();
        if (amount.IsZero || !amount.IsGreaterThan(Money.Zero(amount.Currency)))
            throw new DomainException("Credit amount must be greater than zero");

        Balance = Balance.Add(amount);
        SetUpdated();
        AddDomainEvent(new WalletCreditedEvent(Id, amount, Balance, description));
    }

    /// <summary>
    /// Debits the wallet by <paramref name="amount"/>.
    /// Raises <see cref="WalletDebitedEvent"/>.
    /// </summary>
    /// <param name="amount">Positive monetary amount in the same currency as the wallet.</param>
    /// <param name="description">Human-readable description of the debit reason.</param>
    /// <exception cref="WalletLockedException">Thrown when the wallet is locked.</exception>
    /// <exception cref="DomainException">Thrown when <paramref name="amount"/> is zero or negative.</exception>
    /// <exception cref="InsufficientFundsException">Thrown when the balance would go below zero.</exception>
    public void Debit(Money amount, string description)
    {
        if (IsLocked)
            throw new WalletLockedException();
        if (amount.IsZero || !amount.IsGreaterThan(Money.Zero(amount.Currency)))
            throw new DomainException("Debit amount must be greater than zero");
        if (!Balance.IsGreaterThanOrEqual(amount))
            throw new InsufficientFundsException();

        Balance = Balance.Subtract(amount);
        SetUpdated();
        AddDomainEvent(new WalletDebitedEvent(Id, amount, Balance, description));
    }

    /// <summary>
    /// Locks the wallet, preventing any further credits or debits.
    /// Raises <see cref="WalletLockedEvent"/>.
    /// </summary>
    /// <param name="reason">Optional human-readable reason for the lock.</param>
    public void Lock(string? reason = null)
    {
        IsLocked = true;
        SetUpdated();
        AddDomainEvent(new WalletLockedEvent(Id, reason));
    }

    /// <summary>
    /// Unlocks the wallet, re-enabling credits and debits.
    /// Raises <see cref="WalletLockedEvent"/> with a null reason.
    /// </summary>
    public void Unlock()
    {
        IsLocked = false;
        SetUpdated();
        AddDomainEvent(new WalletLockedEvent(Id, null));
    }
}
