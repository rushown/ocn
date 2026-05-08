using EWallet.Domain.Entities;

namespace EWallet.Domain.Interfaces;

/// <summary>Generic repository contract for <see cref="BaseEntity"/> sub-types.</summary>
/// <typeparam name="T">The entity type managed by this repository.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Returns the entity with <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all entities of type <typeparamref name="T"/>.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Queues an <paramref name="entity"/> for insertion on the next <c>SaveChanges</c>.</summary>
    Task AddAsync(T entity, CancellationToken ct = default);

    /// <summary>Marks <paramref name="entity"/> as modified for the next <c>SaveChanges</c>.</summary>
    void Update(T entity);

    /// <summary>Marks <paramref name="entity"/> for removal on the next <c>SaveChanges</c>.</summary>
    void Delete(T entity);
}

/// <summary>Repository contract for <see cref="Wallet"/> entities.</summary>
public interface IWalletRepository : IRepository<Wallet>
{
    /// <summary>Returns the wallet belonging to <paramref name="userId"/>, or <c>null</c>.</summary>
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the wallet and acquires a pessimistic database-level lock for the duration of the transaction.
    /// Use for debit/credit operations that require serialised access.
    /// </summary>
    Task<Wallet?> GetByIdWithLockAsync(Guid walletId, CancellationToken ct = default);

    /// <summary>Returns the total amount debited from <paramref name="walletId"/> today (UTC).</summary>
    Task<decimal> GetTotalDebitTodayAsync(Guid walletId, CancellationToken ct = default);
}

/// <summary>Repository contract for <see cref="Transaction"/> entities.</summary>
public interface ITransactionRepository : IRepository<Transaction>
{
    /// <summary>Returns a paginated list of transactions for <paramref name="walletId"/>, newest first.</summary>
    Task<IReadOnlyList<Transaction>> GetByWalletIdAsync(Guid walletId, int page, int size, CancellationToken ct = default);

    /// <summary>Returns the transaction with the given <paramref name="key"/>, or <c>null</c> if none exists.</summary>
    Task<Transaction?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Returns the total debited amount for <paramref name="walletId"/> on <paramref name="date"/> (UTC date only).</summary>
    Task<decimal> GetDailyDebitSumAsync(Guid walletId, DateTime date, CancellationToken ct = default);
}

/// <summary>Repository contract for <see cref="User"/> entities.</summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>Returns the user whose email matches <paramref name="email"/> (case-insensitive), or <c>null</c>.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Returns the user who currently holds <paramref name="token"/> as their refresh token, or <c>null</c>.</summary>
    Task<User?> GetByRefreshTokenAsync(string token, CancellationToken ct = default);
}

/// <summary>Repository contract for <see cref="AuditLog"/> entities.</summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IReadOnlyList<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}