using FluentAssertions;
using EWallet.Domain.Enums;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;
using EWallet.Domain.Tests.Helpers;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Tests.Tests;

public class TransactionTests
{
    // ─── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_NewTransaction_HasPendingStatusAndNullCompletedAt()
    {
        var money = new Money(100m, "USD");

        var tx = Transaction.Create(Guid.NewGuid(), money, TransactionType.Transfer, "key-001");

        tx.Status.Should().Be(TransactionStatus.Pending,
            because: "every new transaction starts in the Pending state");
        tx.CompletedAt.Should().BeNull(
            because: "a Pending transaction has not yet completed");
    }

    [Fact]
    public void Create_NewTransaction_SetsIdempotencyKeyAndAmount()
    {
        var money = new Money(42m, "USD");

        var tx = Transaction.Create(Guid.NewGuid(), money, TransactionType.Deposit, "idem-key");

        tx.IdempotencyKey.Should().Be("idem-key");
        tx.Amount.Should().Be(money);
    }

    // ─── Complete ────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_PendingTransaction_SetsStatusCompletedAndSetsCompletedAt()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Pending).Build();

        tx.Complete();

        tx.Status.Should().Be(TransactionStatus.Completed);
        tx.CompletedAt.Should().NotBeNull(
            because: "completing a transaction must record the timestamp");
        tx.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Complete_PendingTransaction_RaisesTransactionStatusChangedEvent()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Pending).Build();

        tx.Complete();

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionStatusChangedEvent,
            because: "a status transition must always publish a domain event");
    }

    [Fact]
    public void Complete_FailedTransaction_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Failed).Build();

        var act = () => tx.Complete();

        act.Should().Throw<InvalidTransactionStateException>(
            because: "a Failed transaction cannot transition to Completed");
    }

    [Fact]
    public void Complete_AlreadyCompletedTransaction_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Completed).Build();

        var act = () => tx.Complete();

        act.Should().Throw<InvalidTransactionStateException>(
            because: "re-completing a transaction is not a valid state transition");
    }

    // ─── Fail ────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_PendingTransaction_SetsStatusFailedAndRecordsReason()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Pending).Build();

        tx.Fail("gateway error");

        tx.Status.Should().Be(TransactionStatus.Failed);
        tx.FailureReason.Should().Be("gateway error");
    }

    [Fact]
    public void Fail_PendingTransaction_RaisesTransactionStatusChangedEvent()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Pending).Build();

        tx.Fail("gateway error");

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionStatusChangedEvent);
    }

    [Fact]
    public void Fail_CompletedTransaction_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Completed).Build();

        var act = () => tx.Fail("late failure");

        act.Should().Throw<InvalidTransactionStateException>(
            because: "a Completed transaction cannot be moved to Failed");
    }

    // ─── Refund ──────────────────────────────────────────────────────────────

    [Fact]
    public void Refund_CompletedTransaction_SetsStatusRefunded()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Completed).Build();

        tx.Refund();

        tx.Status.Should().Be(TransactionStatus.Refunded);
    }

    [Fact]
    public void Refund_CompletedTransaction_RaisesTransactionStatusChangedEvent()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Completed).Build();

        tx.Refund();

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionStatusChangedEvent);
    }

    [Fact]
    public void Refund_PendingTransaction_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Pending).Build();

        var act = () => tx.Refund();

        act.Should().Throw<InvalidTransactionStateException>(
            because: "only Completed transactions can be refunded");
    }

    [Fact]
    public void Refund_FailedTransaction_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Failed).Build();

        var act = () => tx.Refund();

        act.Should().Throw<InvalidTransactionStateException>(
            because: "a Failed transaction has never settled and cannot be refunded");
    }

    [Fact]
    public void Refund_AlreadyRefundedTransaction_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().InState(TransactionStatus.Refunded).Build();

        var act = () => tx.Refund();

        act.Should().Throw<InvalidTransactionStateException>(
            because: "double-refund must be rejected");
    }

    // ─── State machine summary ────────────────────────────────────────────────
    // Valid transitions:   Pending → Completed, Pending → Failed,
    //                      Completed → Refunded
    // Invalid transitions: Failed → Completed, Completed → Completed,
    //                      Pending → Refunded, Failed → Refunded,
    //                      Refunded → *
}
