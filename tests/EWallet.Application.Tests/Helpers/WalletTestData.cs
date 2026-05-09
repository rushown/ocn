using EWallet.Domain.Entities;
using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;

namespace EWallet.Application.Tests.Helpers;

/// <summary>
/// Static factory methods for common domain entity test scenarios.
/// Keep test data centralised here so individual tests stay readable.
/// </summary>
public static class WalletTestData
{
    public static User CreateActiveUser(string email = "user@ewallet.test", string passwordHash = "hash")
        => User.Create(email, phone: "+15551234567", fullName: "Test User", passwordHash: passwordHash);

    public static User CreateInactiveUser(string email = "inactive@ewallet.test", string passwordHash = "hash")
    {
        var u = CreateActiveUser(email, passwordHash);
        u.Deactivate();
        return u;
    }

    public static Wallet CreateWalletForUser(Guid userId, decimal balance = 0m, string currency = "USD")
    {
        var w = Wallet.Create(userId, currency);
        if (balance > 0)
            w.Credit(new Money(balance, currency), "Seed");
        return w;
    }
}
