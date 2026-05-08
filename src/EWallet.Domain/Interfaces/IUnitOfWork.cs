namespace EWallet.Domain.Interfaces;

/// <summary>
/// Coordinates multiple repository operations within a single atomic database transaction.
/// Implementations must ensure that <see cref="SaveChangesAsync"/> dispatches all queued
/// domain events before (or after) flushing to the database, per the chosen consistency model.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Wallet repository scoped to the current unit of work.</summary>
    IWalletRepository Wallets { get; }

    /// <summary>Transaction repository scoped to the current unit of work.</summary>
    ITransactionRepository Transactions { get; }

    /// <summary>User repository scoped to the current unit of work.</summary>
    IUserRepository Users { get; }

    IAuditLogRepository AuditLogs { get; }
    /// <summary>Flushes all pending changes to the database and returns the number of rows affected.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Begins an explicit database transaction. Use when multiple <c>SaveChanges</c> calls must be atomic.</summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>Commits the active explicit transaction.</summary>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>Rolls back the active explicit transaction.</summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
