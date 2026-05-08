using EWallet.Domain.Interfaces;
using EWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace EWallet.Infrastructure.Persistence;

/// <summary>
/// Coordinates multiple repository operations within a single atomic database transaction.
/// Wraps <see cref="AppDbContext"/> and exposes typed repositories as properties.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    /// <inheritdoc />
    public IWalletRepository Wallets { get; }

    /// <inheritdoc />
    public ITransactionRepository Transactions { get; }

    /// <inheritdoc />
    public IUserRepository Users { get; }

    public IAuditLogRepository AuditLogs { get; }

    /// <summary>
    /// Initializes a new <see cref="UnitOfWork"/> with the scoped <see cref="AppDbContext"/>
    /// and concrete repository implementations.
    /// </summary>
    public UnitOfWork(
        AppDbContext context,
        IWalletRepository wallets,
        ITransactionRepository transactions,
        IUserRepository users,
        IAuditLogRepository auditLogs)
    {
        _context = context;
        Wallets = wallets;
        Transactions = transactions;
        Users = users;
        AuditLogs = auditLogs;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _context.Database.BeginTransactionAsync(ct);

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
        if (_transaction is not null)
            await _transaction.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync(ct);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
    }
}
