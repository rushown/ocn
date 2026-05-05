using EWallet.Domain.Entities;
using EWallet.Domain.Enums;
using EWallet.Domain.Interfaces;
using EWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EWallet.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IWalletRepository"/>.
/// Supports pessimistic locking via <c>SELECT ... FOR UPDATE NOWAIT</c> for debit/credit operations.
/// </summary>
public sealed class WalletRepository : BaseRepository<Wallet>, IWalletRepository
{
    private readonly ILogger<WalletRepository> _logger;

    /// <summary>Initializes a new <see cref="WalletRepository"/>.</summary>
    public WalletRepository(AppDbContext context, ILogger<WalletRepository> logger)
        : base(context)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

    /// <summary>
    /// Retrieves the wallet and immediately acquires a row-level lock using
    /// <c>SELECT ... FOR UPDATE NOWAIT</c>. Must be called within an active database transaction.
    /// Returns <c>null</c> if the wallet does not exist; throws if the lock cannot be acquired.
    /// </summary>
    public async Task<Wallet?> GetByIdWithLockAsync(Guid walletId, CancellationToken ct = default)
    {
        // Use raw SQL to issue a PostgreSQL-level row lock
        // EF Core tracks the returned entity so subsequent Update() calls are detected correctly
        var wallets = await _dbSet
            .FromSqlRaw(
                """
                SELECT w.* FROM wallets w
                WHERE w."Id" = {0}
                FOR UPDATE NOWAIT
                """,
                walletId)
            .ToListAsync(ct);

        return wallets.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<decimal> GetTotalDebitTodayAsync(Guid walletId, CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var sum = await _context.Transactions
            .AsNoTracking()
            .Where(t =>
                t.WalletId == walletId &&
                (t.Type == TransactionType.Withdrawal || t.Type == TransactionType.Transfer) &&
                t.Status == TransactionStatus.Completed &&
                t.CreatedAt >= todayUtc)
            .SumAsync(t => (decimal?)t.Amount.Amount, ct);

        return sum ?? 0m;
    }
}
