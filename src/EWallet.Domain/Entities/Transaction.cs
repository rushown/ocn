using EWallet.Domain.Enums;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Entities;

/// <summary>
/// Records a single debit, credit, transfer, or refund against a wallet.
/// Status transitions follow a strict state machine; illegal transitions throw
/// <see cref="InvalidTransactionStateException"/>.
/// </summary>
public sealed class Transaction : BaseEntity
{
    /// <summary>The wallet this transaction is primarily associated with.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>The sender wallet ID for transfer transactions; otherwise <c>null</c>.</summary>
    public Guid? SenderWalletId { get; private set; }

    /// <summary>The receiver wallet ID for transfer transactions; otherwise <c>null</c>.</summary>
    public Guid? ReceiverWalletId { get; private set; }

    /// <summary>The transaction amount.</summary>
    public Money Amount { get; private set; } = default!;

    /// <summary>The type of this transaction.</summary>
    public TransactionType Type { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public TransactionStatus Status { get; private set; } = TransactionStatus.Pending;

    /// <summary>Human-readable description of the transaction.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Client-supplied idempotency key. Used to detect and deduplicate duplicate requests.
    /// </summary>
    public string IdempotencyKey { get; private set; } = default!;

    /// <summary>Optional reference returned by an external payment provider.</summary>
    public string? ExternalReference { get; private set; }

    /// <summary>
    /// EF Core concurrency token. Set by the database; do not assign manually.
    /// </summary>
    public byte[]? RowVersion { get; private set; }

    /// <summary>UTC timestamp when the transaction moved to a terminal state, or <c>null</c> if still pending.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Human-readable failure reason, populated when <see cref="Status"/> is <see cref="TransactionStatus.Failed"/>.</summary>
    public string? FailureReason { get; private set; }

    // EF Core parameterless constructor
    private Transaction() { }

    /// <summary>
    /// Creates a new <see cref="Transaction"/> in <see cref="TransactionStatus.Pending"/> state.
    /// Raises <see cref="TransactionCreatedEvent"/>.
    /// </summary>
    /// <param name="walletId">Primary wallet associated with this transaction.</param>
    /// <param name="amount">The transaction amount.</param>
    /// <param name="type">The transaction type.</param>
    /// <param name="idempotencyKey">Client-supplied deduplication key.</param>
    /// <param name="description">Optional human-readable description.</param>
    /// <returns>A pending <see cref="Transaction"/>.</returns>
    public static Transaction Create(
        Guid walletId,
        Money amount,
        TransactionType type,
        string idempotencyKey,
        string? description = null)
    {
        if (walletId == Guid.Empty)
            throw new DomainException("WalletId cannot be empty");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("IdempotencyKey is required");

        var tx = new Transaction
        {
            WalletId = walletId,
            Amount = amount,
            Type = type,
            IdempotencyKey = idempotencyKey,
            Description = description,
            Status = TransactionStatus.Pending
        };

        tx.AddDomainEvent(new TransactionCreatedEvent(tx.Id, walletId, amount, type));
        return tx;
    }

    /// <summary>
    /// Sets the sender and receiver wallet IDs for a transfer transaction.
    /// </summary>
    /// <param name="senderWalletId">Source wallet.</param>
    /// <param name="receiverWalletId">Destination wallet.</param>
    public void SetTransferParties(Guid senderWalletId, Guid receiverWalletId)
    {
        SenderWalletId = senderWalletId;
        ReceiverWalletId = receiverWalletId;
    }

    /// <summary>
    /// Stores an external payment-provider reference against this transaction.
    /// </summary>
    public void SetExternalReference(string reference) => ExternalReference = reference;

    /// <summary>
    /// Transitions the transaction from <see cref="TransactionStatus.Pending"/> to
    /// <see cref="TransactionStatus.Completed"/>.
    /// Raises <see cref="TransactionStatusChangedEvent"/>.
    /// </summary>
    /// <exception cref="InvalidTransactionStateException">Thrown when the current status is not Pending.</exception>
    public void Complete()
    {
        EnsureStatus(TransactionStatus.Pending, TransactionStatus.Completed);
        var oldStatus = Status;
        Status = TransactionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new TransactionStatusChangedEvent(Id, oldStatus, Status));
    }

    /// <summary>
    /// Transitions the transaction from <see cref="TransactionStatus.Pending"/> to
    /// <see cref="TransactionStatus.Failed"/>.
    /// Raises <see cref="TransactionStatusChangedEvent"/>.
    /// </summary>
    /// <param name="reason">Human-readable explanation of the failure.</param>
    /// <exception cref="InvalidTransactionStateException">Thrown when the current status is not Pending.</exception>
    public void Fail(string reason)
    {
        EnsureStatus(TransactionStatus.Pending, TransactionStatus.Failed);
        var oldStatus = Status;
        Status = TransactionStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new TransactionStatusChangedEvent(Id, oldStatus, Status));
    }

    /// <summary>
    /// Transitions the transaction from <see cref="TransactionStatus.Completed"/> to
    /// <see cref="TransactionStatus.Refunded"/>.
    /// Raises <see cref="TransactionStatusChangedEvent"/>.
    /// </summary>
    /// <exception cref="InvalidTransactionStateException">Thrown when the current status is not Completed.</exception>
    public void Refund()
    {
        EnsureStatus(TransactionStatus.Completed, TransactionStatus.Refunded);
        var oldStatus = Status;
        Status = TransactionStatus.Refunded;
        AddDomainEvent(new TransactionStatusChangedEvent(Id, oldStatus, Status));
    }

    /// <summary>
    /// Transitions the transaction from <see cref="TransactionStatus.Completed"/> to
    /// <see cref="TransactionStatus.Reversed"/>.
    /// Raises <see cref="TransactionStatusChangedEvent"/>.
    /// </summary>
    /// <param name="reason">Human-readable reason for the reversal.</param>
    /// <exception cref="InvalidTransactionStateException">Thrown when the current status is not Completed.</exception>
    public void Reverse(string reason)
    {
        EnsureStatus(TransactionStatus.Completed, TransactionStatus.Reversed);
        var oldStatus = Status;
        Status = TransactionStatus.Reversed;
        FailureReason = reason;
        AddDomainEvent(new TransactionStatusChangedEvent(Id, oldStatus, Status));
    }

    private void EnsureStatus(TransactionStatus required, TransactionStatus target)
    {
        if (Status != required)
            throw new InvalidTransactionStateException(Status.ToString(), target.ToString());
    }
}
