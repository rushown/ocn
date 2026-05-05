using EWallet.Domain.Entities;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Tests.Helpers;

/// <summary>
/// Fluent builder for <see cref="Wallet"/> test fixtures.
/// Constructs wallets in specific states without coupling tests to the
/// entity's constructor signature.
/// </summary>
/// <example>
/// var wallet = new WalletBuilder()
///     .WithBalance(100m)
///     .WithCurrency("USD")
///     .Locked()
///     .Build();
/// </example>
public sealed class WalletBuilder
{
    private Guid   _ownerId  = Guid.NewGuid();
    private decimal _balance  = 0m;
    private string  _currency = "USD";
    private bool    _locked   = false;

    public WalletBuilder WithOwner(Guid ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public WalletBuilder WithBalance(decimal amount)
    {
        _balance = amount;
        return this;
    }

    public WalletBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    /// <summary>Marks the wallet as administratively locked.</summary>
    public WalletBuilder Locked()
    {
        _locked = true;
        return this;
    }

    public Wallet Build()
    {
        var wallet = Wallet.Create(_ownerId);

        if (_balance > 0)
            wallet.Credit(new Money(_balance, _currency), "test-setup");

        if (_locked)
            wallet.Lock();

        // Clear domain events accumulated during setup so individual tests
        // only observe events raised by the action under test.
        wallet.ClearDomainEvents();

        return wallet;
    }
}
