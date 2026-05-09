using EWallet.Domain.Entities;
using EWallet.Domain.Interfaces;
using EWallet.Domain.Enums;
using EWallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EWallet.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IUserRepository"/>.
/// Note: the global query filter in <see cref="UserConfiguration"/> restricts results to
/// active users (<c>IsActive == true</c>) unless <c>IgnoreQueryFilters()</c> is applied.
/// </summary>
public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    /// <summary>Initializes a new <see cref="UserRepository"/>.</summary>
    public UserRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalised = email.Trim().ToLowerInvariant();
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalised, ct);
    }

    /// <inheritdoc />
    public async Task<User?> GetByRefreshTokenAsync(string token, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.RefreshToken == token && u.RefreshTokenExpiry > DateTime.UtcNow,
                ct);

    /// <inheritdoc />
    public async Task<bool> ValidateOtpAsync(Guid userId, string code, OtpPurpose purpose, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return false;
        if (string.IsNullOrWhiteSpace(code)) return false;

        var otp = await _context.OtpRecords
            .FirstOrDefaultAsync(o =>
                o.UserId == userId &&
                o.Code == code &&
                o.Purpose == purpose &&
                !o.IsUsed &&
                o.ExpiresAt > DateTime.UtcNow,
                ct);

        if (otp is null)
            return false;

        otp.MarkUsed();
        return true;
    }
}
