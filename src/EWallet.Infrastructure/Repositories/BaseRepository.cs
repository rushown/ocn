using EWallet.Domain.Entities;
using EWallet.Domain.Interfaces;
using EWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EWallet.Infrastructure.Repositories;

/// <summary>
/// Generic repository base providing standard CRUD operations against <see cref="AppDbContext"/>.
/// Concrete repositories extend this class and add domain-specific query methods.
/// </summary>
/// <typeparam name="T">The <see cref="BaseEntity"/> sub-type managed by this repository.</typeparam>
public abstract class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    /// <summary>The shared <see cref="AppDbContext"/> for the current unit of work scope.</summary>
    protected readonly AppDbContext _context;

    /// <summary>The typed <see cref="DbSet{T}"/> for convenient access.</summary>
    protected readonly DbSet<T> _dbSet;

    /// <summary>Initializes the repository with the scoped <see cref="AppDbContext"/>.</summary>
    protected BaseRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    /// <inheritdoc />
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(e => e.Id == id, ct);

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.ToListAsync(ct);

    /// <inheritdoc />
    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await _dbSet.AddAsync(entity, ct);

    /// <inheritdoc />
    public virtual void Update(T entity) => _dbSet.Update(entity);

    /// <inheritdoc />
    public virtual void Delete(T entity) => _dbSet.Remove(entity);
}
