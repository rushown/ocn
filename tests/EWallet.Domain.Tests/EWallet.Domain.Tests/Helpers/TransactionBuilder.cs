namespace EWallet.Domain.Tests.Helpers;

/// <summary>
/// Fluent test-data builder for <see cref="Transaction"/>.
/// Allows constructing transactions already in a specific state.
/// </summary>
public sealed class TransactionBuilder
{
    private Guid _walletId = Guid.NewGuid();
    private Money _money = new(100m, "USD");
    private TransactionType _type = TransactionType.Transfer;
    private string _idempotencyKey = Guid.NewGuid().ToString();
    private TransactionState _targetState = TransactionState.Pending;
    private string? _failureReason;

    public TransactionBuilder WithWalletId(Guid walletId)
    {
        _walletId = walletId;
        return this;
    }

    public TransactionBuilder WithMoney(Money money)
    {
        _money = money;
        return this;
    }

    public TransactionBuilder WithAmount(decimal amount, string currency = "USD")
    {
        _money = new Money(amount, currency);
        return this;
    }

    public TransactionBuilder WithType(TransactionType type)
    {
        _type = type;
        return this;
    }

    public TransactionBuilder WithIdempotencyKey(string key)
    {
        _idempotencyKey = key;
        return this;
    }

    public TransactionBuilder AsPending()
    {
        _targetState = TransactionState.Pending;
        return this;
    }

    public TransactionBuilder AsCompleted()
    {
        _targetState = TransactionState.Completed;
        return this;
    }

    public TransactionBuilder AsFailed(string reason = "test failure")
    {
        _targetState = TransactionState.Failed;
        _failureReason = reason;
        return this;
    }

    public TransactionBuilder AsRefunded()
    {
        _targetState = TransactionState.Refunded;
        return this;
    }

    public Transaction Build()
    {
        var tx = Transaction.Create(_walletId, _money, _type, _idempotencyKey);

        switch (_targetState)
        {
            case TransactionState.Completed:
                tx.Complete();
                break;

            case TransactionState.Failed:
                tx.Fail(_failureReason ?? "test failure");
                break;

            case TransactionState.Refunded:
                tx.Complete();
                tx.Refund();
                break;

            case TransactionState.Pending:
            default:
                break;
        }

        tx.ClearDomainEvents();
        return tx;
    }
}
