using EWallet.Domain.Entities;
using EWallet.Domain.Enums;

namespace EWallet.Application.Tests.Helpers;

/// <summary>
/// Static factory methods for common domain entity test scenarios.
/// Keep test data centralised here so individual tests stay readable.
/// </summary>
public static class WalletTestData
{
    // ─── Wallets ────────────────────────────────────────────────────────────

    /// <summary>A normal wallet with sufficient funds.</summary>
    public static Wallet CreateSolventWallet(decimal balance = 1_000m) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Balance = balance,
        Currency = "USD",
        IsLocked = false,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>A wallet with zero balance — any debit will trigger INSUFFICIENT_FUNDS.</summary>
    public static Wallet CreateInsolventWallet() => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Balance = 0m,
        Currency = "USD",
        IsLocked = false,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>A wallet that has been administratively locked.</summary>
    public static Wallet CreateLockedWallet(decimal balance = 500m) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Balance = balance,
        Currency = "USD",
        IsLocked = true,
        CreatedAt = DateTime.UtcNow,
    };

    // ─── Users ──────────────────────────────────────────────────────────────

    /// <summary>Active Tier-1 user (daily transfer limit $1 000).</summary>
    public static User CreateTier1User(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = "tier1user@ewallet.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("P@ssword1!"),
        IsActive = isActive,
        Tier = UserTier.Tier1,
        DailyTransferLimit = 1_000m,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>Active Tier-2 user (daily transfer limit $10 000).</summary>
    public static User CreateTier2User(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = "tier2user@ewallet.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("P@ssword1!"),
        IsActive = isActive,
        Tier = UserTier.Tier2,
        DailyTransferLimit = 10_000m,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>Creates a matched (user, wallet) pair owned by the same identity.</summary>
    public static (User user, Wallet wallet) CreateUserWithWallet(
        decimal balance = 1_000m,
        UserTier tier = UserTier.Tier1)
    {
        var user = tier == UserTier.Tier2 ? CreateTier2User() : CreateTier1User();
        var wallet = CreateSolventWallet(balance);
        wallet.OwnerId = user.Id;
        return (user, wallet);
    }
}
