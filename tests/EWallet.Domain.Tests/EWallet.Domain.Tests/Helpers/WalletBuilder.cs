namespace EWallet.Domain.Tests.Helpers;

/// <summary>
/// Fluent test-data builder for <see cref="Wallet"/>.
/// </summary>
public sealed class WalletBuilder
{
    private Guid _userId = Guid.NewGuid();
    private decimal _balance = 0m;
    private string _currency = "USD";
    private bool _locked = false;

    public WalletBuilder WithUserId(Guid userId)
    {
        _userId = userId;
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

    public WalletBuilder Locked()
    {
        _locked = true;
        return this;
    }

    public Wallet Build()
    {
        var wallet = Wallet.Create(_userId);

        if (_balance > 0)
        {
            wallet.Credit(new Money(_balance, _currency), "test-seed");
        }

        if (_locked)
        {
            wallet.Lock();
        }

        // Clear domain events so tests start with a clean slate
        wallet.ClearDomainEvents();

        return wallet;
    }
}
