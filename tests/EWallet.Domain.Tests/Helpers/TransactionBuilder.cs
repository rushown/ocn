using EWallet.Domain.Entities;
using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Tests.Helpers;

/// <summary>
/// Fluent builder for <see cref="Transaction"/> test fixtures.
/// Allows tests to start with a transaction already in a desired state
/// (Pending, Completed, Failed, Refunded) without repeating state-transition
/// boilerplate in every test.
/// </summary>
/// <example>
/// var tx = new TransactionBuilder()
///     .WithAmount(100m)
///     .InState(TransactionStatus.Completed)
///     .Build();
/// </example>
public sealed class TransactionBuilder
{
    private Guid              _walletId       = Guid.NewGuid();
    private decimal           _amount         = 50m;
    private string            _currency       = "USD";
    private TransactionType   _type           = TransactionType.Transfer;
    private string            _idempotencyKey = Guid.NewGuid().ToString();
    private TransactionStatus _targetState    = TransactionStatus.Pending;
    private string?           _failureReason  = null;

    public TransactionBuilder WithWallet(Guid walletId)
    {
        _walletId = walletId;
        return this;
    }

    public TransactionBuilder WithAmount(decimal amount, string currency = "USD")
    {
        _amount   = amount;
        _currency = currency;
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

    /// <summary>
    /// Drive the transaction to <paramref name="state"/> before returning it.
    /// </summary>
    public TransactionBuilder InState(TransactionStatus state, string? failureReason = null)
    {
        _targetState   = state;
        _failureReason = failureReason;
        return this;
    }

    public Transaction Build()
    {
        var money = new Money(_amount, _currency);
        var tx    = Transaction.Create(_walletId, money, _type, _idempotencyKey);

        switch (_targetState)
        {
            case TransactionStatus.Completed:
                tx.Complete();
                break;

            case TransactionStatus.Failed:
                tx.Fail(_failureReason ?? "test-induced failure");
                break;

            case TransactionStatus.Refunded:
                tx.Complete();
                tx.Refund();
                break;

            case TransactionStatus.Pending:
            default:
                break; // already Pending after Create
        }

        // Clear domain events so tests only see events from their own action.
        tx.ClearDomainEvents();

        return tx;
    }
}
