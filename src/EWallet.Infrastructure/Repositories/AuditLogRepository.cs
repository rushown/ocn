using EWallet.Domain.Entities;
using EWallet.Domain.Interfaces;
using EWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EWallet.Infrastructure.Repositories;

public sealed class AuditLogRepository : BaseRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(a => a.PerformedByUserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct);
}