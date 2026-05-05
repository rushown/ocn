using EWallet.Domain.Entities;
using EWallet.Domain.Interfaces;
using EWallet.Domain.Enums;
using EWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EWallet.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="ITransactionRepository"/>.
/// All read-only queries use <c>AsNoTracking()</c> for performance.
/// </summary>
public sealed class TransactionRepository : BaseRepository<Transaction>, ITransactionRepository
{
    /// <summary>Initializes a new <see cref="TransactionRepository"/>.</summary>
    public TransactionRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetByWalletIdAsync(
        Guid walletId,
        int page,
        int size,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 20;

        return await _dbSet
            .AsNoTracking()
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Transaction?> GetByIdempotencyKeyAsync(
        string key,
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);

    /// <inheritdoc />
    public async Task<decimal> GetDailyDebitSumAsync(
        Guid walletId,
        DateTime date,
        CancellationToken ct = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var sum = await _dbSet
            .AsNoTracking()
            .Where(t =>
                t.WalletId == walletId &&
                (t.Type == TransactionType.Withdrawal || t.Type == TransactionType.Transfer) &&
                t.Status == TransactionStatus.Completed &&
                t.CreatedAt >= startOfDay &&
                t.CreatedAt < endOfDay)
            .SumAsync(t => (decimal?)t.Amount.Amount, ct);

        return sum ?? 0m;
    }
}
