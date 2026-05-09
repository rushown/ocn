using Bogus;
using EWallet.API.Models;

namespace EWallet.Integration.Tests.Helpers;

/// <summary>
/// Centralized Bogus-based factory for realistic request payloads.
/// All generators use a fixed locale so test data stays consistent across runs.
/// </summary>
public static class FakeDataFactory
{
    // ─── Auth ─────────────────────────────────────────────────────────────────

    /// <summary>Generates a fully valid registration payload with a strong password.</summary>
    public static EWallet.API.Models.RegisterRequest ValidRegisterRequest()
    {
        var f = new Faker();
        var password = "Test@1234!";
        return new EWallet.API.Models.RegisterRequest(
            Email: f.Internet.Email().ToLowerInvariant(),
            PhoneNumber: $"+1{f.Phone.PhoneNumber("##########")}",
            FullName: f.Name.FullName(),
            Password: password,
            ConfirmPassword: password);
    }

    /// <summary>Generates a registration request with a deliberately weak password.</summary>
    public static EWallet.API.Models.RegisterRequest WeakPasswordRegisterRequest()
    {
        var f = new Faker();
        return new EWallet.API.Models.RegisterRequest(
            Email: f.Internet.Email().ToLowerInvariant(),
            PhoneNumber: $"+1{f.Phone.PhoneNumber("##########")}",
            FullName: f.Name.FullName(),
            Password: "weak",
            ConfirmPassword: "weak");
    }

    /// <summary>Generates a registration request where passwords don't match.</summary>
    public static EWallet.API.Models.RegisterRequest MismatchedPasswordRegisterRequest()
    {
        var f = new Faker();
        return new EWallet.API.Models.RegisterRequest(
            Email: f.Internet.Email().ToLowerInvariant(),
            PhoneNumber: $"+1{f.Phone.PhoneNumber("##########")}",
            FullName: f.Name.FullName(),
            Password: "Test@1234!",
            ConfirmPassword: "Different@5678!");
    }

    // ─── Wallet ───────────────────────────────────────────────────────────────

    /// <summary>Creates a valid deposit request with an optional explicit amount.</summary>
    public static EWallet.API.Models.DepositRequest ValidDepositRequest(decimal amount = 500m)
    {
        var f = new Faker();
        return new EWallet.API.Models.DepositRequest(
            Amount: amount,
            Currency: "USD",
            ExternalRef: $"ext_{f.Random.AlphaNumeric(24)}");
    }

    /// <summary>Creates a valid transfer request targeting a specific receiver wallet.</summary>
    public static EWallet.API.Models.TransferRequest ValidTransferRequest(Guid receiverWalletId, decimal amount = 100m)
        => new(receiverWalletId, amount, "USD", Description: "test transfer", OtpCode: null);
}
