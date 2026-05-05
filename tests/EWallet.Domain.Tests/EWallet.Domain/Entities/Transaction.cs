using EWallet.Domain.Abstractions;
using EWallet.Domain.Enums;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Entities;

public sealed class Transaction : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public Money Amount { get; private set; } = default!;
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Transaction() { }

    public static Transaction Create(
        Guid walletId,
        Money amount,
        TransactionType type,
        string idempotencyKey)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            Amount = amount,
            Type = type,
            Status = TransactionStatus.Pending,
            IdempotencyKey = idempotencyKey
        };
    }

    public void Complete()
    {
        EnsureStatus(TransactionStatus.Pending, nameof(Complete));

        var old = Status;
        Status = TransactionStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new TransactionStatusChangedEvent(Id, old, Status));
    }

    public void Fail(string reason)
    {
        EnsureStatus(TransactionStatus.Pending, nameof(Fail));

        var old = Status;
        Status = TransactionStatus.Failed;
        FailureReason = reason;
        RaiseDomainEvent(new TransactionStatusChangedEvent(Id, old, Status));
    }

    public void Refund()
    {
        EnsureStatus(TransactionStatus.Completed, nameof(Refund));

        var old = Status;
        Status = TransactionStatus.Refunded;
        RaiseDomainEvent(new TransactionStatusChangedEvent(Id, old, Status));
    }

    // -----------------------------------------------------------------------

    private void EnsureStatus(TransactionStatus required, string operation)
    {
        if (Status != required)
            throw new InvalidTransactionStateException(
                $"Cannot {operation} a transaction in '{Status}' state. Required state: '{required}'.");
    }
}
