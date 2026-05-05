using Bogus;
using EWallet.Application.Contracts.Requests;

namespace EWallet.Integration.Tests.Helpers;

/// <summary>
/// Centralized Bogus-based factory for realistic request payloads.
/// All generators use a fixed locale so test data stays consistent across runs.
/// </summary>
public static class FakeDataFactory
{
    // ─── Auth ─────────────────────────────────────────────────────────────────

    /// <summary>Generates a fully valid registration payload with a strong password.</summary>
    public static RegisterRequest ValidRegisterRequest() =>
        new Faker<RegisterRequest>()
            .RuleFor(r => r.Email,           f => f.Internet.Email().ToLowerInvariant())
            .RuleFor(r => r.PhoneNumber,     f => $"+1{f.Phone.PhoneNumber("##########")}")
            .RuleFor(r => r.FullName,        f => f.Name.FullName())
            .RuleFor(r => r.Password,        _ => "Test@1234!")
            .RuleFor(r => r.ConfirmPassword, _ => "Test@1234!")
            .Generate();

    /// <summary>Generates a registration request with a deliberately weak password.</summary>
    public static RegisterRequest WeakPasswordRegisterRequest()
    {
        var req = ValidRegisterRequest();
        req.Password        = "weak";
        req.ConfirmPassword = "weak";
        return req;
    }

    /// <summary>Generates a registration request where passwords don't match.</summary>
    public static RegisterRequest MismatchedPasswordRegisterRequest()
    {
        var req = ValidRegisterRequest();
        req.ConfirmPassword = "Different@5678!";
        return req;
    }

    // ─── Wallet ───────────────────────────────────────────────────────────────

    /// <summary>Creates a valid deposit request with an optional explicit amount.</summary>
    public static DepositRequest ValidDepositRequest(decimal amount = 500m) =>
        new Faker<DepositRequest>()
            .RuleFor(r => r.Amount,       _ => amount)
            .RuleFor(r => r.GatewayToken, f => $"tok_{f.Random.AlphaNumeric(24)}")
            .Generate();

    /// <summary>Creates a valid transfer request targeting a specific receiver wallet.</summary>
    public static TransferRequest ValidTransferRequest(Guid receiverWalletId, decimal amount = 100m) =>
        new Faker<TransferRequest>()
            .RuleFor(r => r.Amount,           _ => amount)
            .RuleFor(r => r.ReceiverWalletId, _ => receiverWalletId)
            .RuleFor(r => r.IdempotencyKey,   f => f.Random.Uuid().ToString())
            .Generate();
}
