namespace EWallet.Domain.Tests.Domain.Transaction;

public sealed class TransactionTests
{
    private static readonly Guid _walletId = Guid.NewGuid();
    private static readonly Money _money = new(100m, "USD");

    // -----------------------------------------------------------------------
    // Transaction.Create
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_InitialStatus_IsPending()
    {
        var tx = Transaction.Create(_walletId, _money, TransactionType.Transfer, "key-1");

        tx.Status.Should().Be(TransactionStatus.Pending);
        tx.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_SetsIdAndWalletId()
    {
        var tx = Transaction.Create(_walletId, _money, TransactionType.Transfer, "key-2");

        tx.Id.Should().NotBeEmpty();
        tx.WalletId.Should().Be(_walletId);
    }

    [Fact]
    public void Create_SetsIdempotencyKey()
    {
        const string key = "unique-key-abc";
        var tx = Transaction.Create(_walletId, _money, TransactionType.Transfer, key);

        tx.IdempotencyKey.Should().Be(key);
    }

    // -----------------------------------------------------------------------
    // Transaction.Complete
    // -----------------------------------------------------------------------

    [Fact]
    public void Complete_FromPending_SetsStatusToCompleted()
    {
        var tx = new TransactionBuilder().AsPending().Build();

        tx.Complete();

        tx.Status.Should().Be(TransactionStatus.Completed);
    }

    [Fact]
    public void Complete_FromPending_SetsCompletedAt()
    {
        var tx = new TransactionBuilder().AsPending().Build();
        var before = DateTimeOffset.UtcNow;

        tx.Complete();

        tx.CompletedAt.Should().NotBeNull()
            .And.BeOnOrAfter(before);
    }

    [Fact]
    public void Complete_FromPending_RaisesTransactionStatusChangedEvent()
    {
        var tx = new TransactionBuilder().AsPending().Build();

        tx.Complete();

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionStatusChangedEvent);
    }

    [Fact]
    public void Complete_FromFailed_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().AsFailed().Build();

        var act = () => tx.Complete();

        act.Should().Throw<InvalidTransactionStateException>();
    }

    [Fact]
    public void Complete_FromRefunded_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().AsRefunded().Build();

        var act = () => tx.Complete();

        act.Should().Throw<InvalidTransactionStateException>();
    }

    [Fact]
    public void Complete_FromCompleted_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().AsCompleted().Build();

        var act = () => tx.Complete();

        act.Should().Throw<InvalidTransactionStateException>();
    }

    // -----------------------------------------------------------------------
    // Transaction.Fail
    // -----------------------------------------------------------------------

    [Fact]
    public void Fail_FromPending_SetsStatusToFailed()
    {
        var tx = new TransactionBuilder().AsPending().Build();

        tx.Fail("gateway error");

        tx.Status.Should().Be(TransactionStatus.Failed);
    }

    [Fact]
    public void Fail_FromPending_SetsFailureReason()
    {
        var tx = new TransactionBuilder().AsPending().Build();

        tx.Fail("gateway error");

        tx.FailureReason.Should().Be("gateway error");
    }

    [Fact]
    public void Fail_FromPending_RaisesTransactionStatusChangedEvent()
    {
        var tx = new TransactionBuilder().AsPending().Build();

        tx.Fail("timeout");

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionStatusChangedEvent);
    }

    [Fact]
    public void Fail_FromCompleted_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().AsCompleted().Build();

        var act = () => tx.Fail("late failure");

        act.Should().Throw<InvalidTransactionStateException>();
    }

    // -----------------------------------------------------------------------
    // Transaction.Refund
    // -----------------------------------------------------------------------

    [Fact]
    public void Refund_FromCompleted_SetsStatusToRefunded()
    {
        var tx = new TransactionBuilder().AsCompleted().Build();

        tx.Refund();

        tx.Status.Should().Be(TransactionStatus.Refunded);
    }

    [Fact]
    public void Refund_FromCompleted_RaisesTransactionStatusChangedEvent()
    {
        var tx = new TransactionBuilder().AsCompleted().Build();

        tx.Refund();

        tx.DomainEvents.Should().ContainSingle(e => e is TransactionStatusChangedEvent);
    }

    [Fact]
    public void Refund_FromPending_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().AsPending().Build();

        var act = () => tx.Refund();

        act.Should().Throw<InvalidTransactionStateException>();
    }

    [Fact]
    public void Refund_FromFailed_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().AsFailed().Build();

        var act = () => tx.Refund();

        act.Should().Throw<InvalidTransactionStateException>();
    }

    [Fact]
    public void Refund_FromRefunded_ThrowsInvalidTransactionStateException()
    {
        var tx = new TransactionBuilder().AsRefunded().Build();

        var act = () => tx.Refund();

        act.Should().Throw<InvalidTransactionStateException>();
    }

    // -----------------------------------------------------------------------
    // State machine — full valid paths [Theory]
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(TransactionType.Transfer)]
    [InlineData(TransactionType.Deposit)]
    [InlineData(TransactionType.Withdrawal)]
    public void Create_WithAnyTransactionType_InitialStatusIsPending(TransactionType type)
    {
        var tx = Transaction.Create(_walletId, _money, type, Guid.NewGuid().ToString());

        tx.Status.Should().Be(TransactionStatus.Pending);
    }
}
