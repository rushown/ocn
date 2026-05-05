using FluentAssertions;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;
using EWallet.Domain.Tests.Helpers;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Tests.Tests;

public class WalletTests
{
    // ─── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_NewWallet_HasZeroBalanceAndIsNotLocked()
    {
        var ownerId = Guid.NewGuid();

        var wallet = Wallet.Create(ownerId);

        wallet.Balance.Amount.Should().Be(0m,
            because: "a brand-new wallet starts with no funds");
        wallet.IsLocked.Should().BeFalse();
        wallet.OwnerId.Should().Be(ownerId);
    }

    // ─── Credit ──────────────────────────────────────────────────────────────

    [Fact]
    public void Credit_ValidAmount_IncreasesBalanceAndRaisesWalletCreditedEvent()
    {
        var wallet = new WalletBuilder().WithBalance(0m).Build();
        var credit = new Money(100m, "USD");

        wallet.Credit(credit, "deposit");

        wallet.Balance.Amount.Should().Be(100m);
        wallet.DomainEvents.Should().ContainSingle(e => e is WalletCreditedEvent,
            because: "every successful credit must raise a WalletCreditedEvent");
    }

    [Fact]
    public void Credit_LockedWallet_ThrowsWalletLockedException()
    {
        var wallet = new WalletBuilder().WithBalance(0m).Locked().Build();

        var act = () => wallet.Credit(new Money(50m, "USD"), "deposit");

        act.Should().Throw<WalletLockedException>(
            because: "credits to a locked wallet are not permitted");
    }

    [Fact]
    public void Credit_ZeroAmount_ThrowsDomainException()
    {
        var wallet = new WalletBuilder().Build();

        var act = () => wallet.Credit(new Money(0m, "USD"), "deposit");

        act.Should().Throw<DomainException>(
            because: "crediting zero is a no-op and likely a caller bug");
    }

    // ─── Debit ───────────────────────────────────────────────────────────────

    [Fact]
    public void Debit_SufficientFunds_DecreasesBalanceAndRaisesWalletDebitedEvent()
    {
        var wallet = new WalletBuilder().WithBalance(100m).Build();
        var debit  = new Money(50m, "USD");

        wallet.Debit(debit, "withdrawal");

        wallet.Balance.Amount.Should().Be(50m);
        wallet.DomainEvents.Should().ContainSingle(e => e is WalletDebitedEvent,
            because: "every successful debit must raise a WalletDebitedEvent");
    }

    [Fact]
    public void Debit_InsufficientFunds_ThrowsInsufficientFundsException()
    {
        var wallet = new WalletBuilder().WithBalance(10m).Build();

        var act = () => wallet.Debit(new Money(50m, "USD"), "withdrawal");

        act.Should().Throw<InsufficientFundsException>(
            because: "a debit larger than the available balance must be rejected");
    }

    [Fact]
    public void Debit_LockedWallet_ThrowsWalletLockedException()
    {
        var wallet = new WalletBuilder().WithBalance(200m).Locked().Build();

        var act = () => wallet.Debit(new Money(10m, "USD"), "withdrawal");

        act.Should().Throw<WalletLockedException>(
            because: "debits from a locked wallet are not permitted");
    }

    [Fact]
    public void Debit_ExactBalance_LeavesZeroBalance()
    {
        var wallet = new WalletBuilder().WithBalance(75m).Build();

        wallet.Debit(new Money(75m, "USD"), "withdrawal");

        wallet.Balance.Amount.Should().Be(0m);
    }

    // ─── Lock ────────────────────────────────────────────────────────────────

    [Fact]
    public void Lock_UnlockedWallet_SetsIsLockedTrueAndRaisesWalletLockedEvent()
    {
        var wallet = new WalletBuilder().Build();

        wallet.Lock();

        wallet.IsLocked.Should().BeTrue();
        wallet.DomainEvents.Should().ContainSingle(e => e is WalletLockedEvent,
            because: "locking a wallet must raise a WalletLockedEvent");
    }

    [Fact]
    public void Lock_AlreadyLockedWallet_ThrowsDomainException()
    {
        var wallet = new WalletBuilder().Locked().Build();

        var act = () => wallet.Lock();

        act.Should().Throw<DomainException>(
            because: "locking an already-locked wallet is a no-op / caller error");
    }

    // ─── Unlock ──────────────────────────────────────────────────────────────

    [Fact]
    public void Unlock_LockedWallet_SetsIsLockedFalse()
    {
        var wallet = new WalletBuilder().Locked().Build();

        wallet.Unlock();

        wallet.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Unlock_AlreadyUnlockedWallet_ThrowsDomainException()
    {
        var wallet = new WalletBuilder().Build(); // not locked

        var act = () => wallet.Unlock();

        act.Should().Throw<DomainException>();
    }
}
