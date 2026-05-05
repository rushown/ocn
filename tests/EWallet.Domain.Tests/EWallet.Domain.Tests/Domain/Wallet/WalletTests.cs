namespace EWallet.Domain.Tests.Domain.Wallet;

public sealed class WalletTests
{
    // -----------------------------------------------------------------------
    // Wallet.Create
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_InitialState_HasZeroBalanceAndIsNotLocked()
    {
        var userId = Guid.NewGuid();

        var wallet = Wallet.Create(userId);

        wallet.Balance.Amount.Should().Be(0m);
        wallet.IsLocked.Should().BeFalse();
        wallet.UserId.Should().Be(userId);
    }

    [Fact]
    public void Create_SetsIdToNonEmpty()
    {
        var wallet = Wallet.Create(Guid.NewGuid());
        wallet.Id.Should().NotBeEmpty();
    }

    // -----------------------------------------------------------------------
    // Wallet.Credit
    // -----------------------------------------------------------------------

    [Fact]
    public void Credit_ValidAmount_IncreasesBalance()
    {
        var wallet = new WalletBuilder().Build();

        wallet.Credit(new Money(100m, "USD"), "deposit");

        wallet.Balance.Amount.Should().Be(100m);
    }

    [Fact]
    public void Credit_RaisesWalletCreditedEvent()
    {
        var wallet = new WalletBuilder().Build();

        wallet.Credit(new Money(100m, "USD"), "deposit");

        wallet.DomainEvents.Should().ContainSingle(e => e is WalletCreditedEvent);
    }

    [Fact]
    public void Credit_ZeroAmount_ThrowsDomainException()
    {
        var wallet = new WalletBuilder().Build();

        var act = () => wallet.Credit(new Money(0m, "USD"), "deposit");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Credit_LockedWallet_ThrowsWalletLockedException()
    {
        var wallet = new WalletBuilder().Locked().Build();

        var act = () => wallet.Credit(new Money(10m, "USD"), "deposit");

        act.Should().Throw<WalletLockedException>();
    }

    // -----------------------------------------------------------------------
    // Wallet.Debit
    // -----------------------------------------------------------------------

    [Fact]
    public void Debit_SufficientFunds_DecreasesBalance()
    {
        var wallet = new WalletBuilder().WithBalance(100m).Build();

        wallet.Debit(new Money(50m, "USD"), "withdrawal");

        wallet.Balance.Amount.Should().Be(50m);
    }

    [Fact]
    public void Debit_SufficientFunds_RaisesWalletDebitedEvent()
    {
        var wallet = new WalletBuilder().WithBalance(100m).Build();

        wallet.Debit(new Money(50m, "USD"), "withdrawal");

        wallet.DomainEvents.Should().ContainSingle(e => e is WalletDebitedEvent);
    }

    [Fact]
    public void Debit_InsufficientFunds_ThrowsInsufficientFundsException()
    {
        var wallet = new WalletBuilder().WithBalance(10m).Build();

        var act = () => wallet.Debit(new Money(50m, "USD"), "withdrawal");

        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void Debit_LockedWallet_ThrowsWalletLockedException()
    {
        var wallet = new WalletBuilder()
            .WithBalance(100m)
            .Locked()
            .Build();

        var act = () => wallet.Debit(new Money(10m, "USD"), "withdrawal");

        act.Should().Throw<WalletLockedException>();
    }

    [Fact]
    public void Debit_ExactBalance_ResultsInZeroBalance()
    {
        var wallet = new WalletBuilder().WithBalance(50m).Build();

        wallet.Debit(new Money(50m, "USD"), "withdrawal");

        wallet.Balance.Amount.Should().Be(0m);
    }

    // -----------------------------------------------------------------------
    // Wallet.Lock
    // -----------------------------------------------------------------------

    [Fact]
    public void Lock_UnlockedWallet_SetsIsLockedTrue()
    {
        var wallet = new WalletBuilder().Build();

        wallet.Lock();

        wallet.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Lock_RaisesWalletLockedEvent()
    {
        var wallet = new WalletBuilder().Build();

        wallet.Lock();

        wallet.DomainEvents.Should().ContainSingle(e => e is WalletLockedEvent);
    }

    [Fact]
    public void Lock_AlreadyLockedWallet_ThrowsDomainException()
    {
        var wallet = new WalletBuilder().Locked().Build();

        var act = () => wallet.Lock();

        act.Should().Throw<DomainException>();
    }

    // -----------------------------------------------------------------------
    // Wallet.Unlock
    // -----------------------------------------------------------------------

    [Fact]
    public void Unlock_LockedWallet_SetsIsLockedFalse()
    {
        var wallet = new WalletBuilder().Locked().Build();

        wallet.Unlock();

        wallet.IsLocked.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // DomainEvents accumulation
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleOperations_AccumulateDomainEvents()
    {
        var wallet = new WalletBuilder().Build();

        wallet.Credit(new Money(100m, "USD"), "deposit");
        wallet.Debit(new Money(30m, "USD"), "withdrawal");

        wallet.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var wallet = new WalletBuilder().Build();
        wallet.Credit(new Money(50m, "USD"), "deposit");

        wallet.ClearDomainEvents();

        wallet.DomainEvents.Should().BeEmpty();
    }
}
