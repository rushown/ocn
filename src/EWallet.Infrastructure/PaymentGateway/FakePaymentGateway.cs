using EWallet.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace EWallet.Infrastructure.PaymentGateway;

/// <summary>
/// Simulated payment gateway for development and testing.
/// Deposits always succeed. Withdrawals succeed 95% of the time. Refunds always succeed.
/// </summary>
/// <remarks>
/// ⚠️ PRODUCTION WARNING: This class must be replaced with a real payment gateway
/// implementation before going live. It is intentionally marked <see cref="ObsoleteAttribute"/>
/// to surface compile-time warnings when referenced outside of development environments.
/// Use an environment guard in <c>DependencyInjection.cs</c> to prevent accidental registration.
/// </remarks>
[Obsolete("FakePaymentGateway is for development only. Replace with a real payment gateway before production.")]
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ILogger<FakePaymentGateway> _logger;

    /// <summary>Initializes the fake payment gateway.</summary>
    public FakePaymentGateway(ILogger<FakePaymentGateway> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>Deposits always succeed in this simulation.</remarks>
    public Task<PaymentResult> ProcessDepositAsync(
        Guid walletId, decimal amount, string currency, CancellationToken ct = default)
    {
        var externalRef = GenerateRef();
        _logger.LogInformation(
            "[FakeGateway] Deposit SUCCESS | Wallet: {WalletId} | Amount: {Amount} {Currency} | Ref: {Ref}",
            walletId, amount, currency, externalRef);

        return Task.FromResult(new PaymentResult(true, externalRef));
    }

    /// <inheritdoc />
    /// <remarks>Withdrawals succeed with a 95% probability.</remarks>
    public Task<PaymentResult> ProcessWithdrawalAsync(
        Guid walletId, decimal amount, string currency, CancellationToken ct = default)
    {
        var success = Random.Shared.NextDouble() < 0.95;
        var externalRef = GenerateRef();

        if (success)
        {
            _logger.LogInformation(
                "[FakeGateway] Withdrawal SUCCESS | Wallet: {WalletId} | Amount: {Amount} {Currency} | Ref: {Ref}",
                walletId, amount, currency, externalRef);

            return Task.FromResult(new PaymentResult(true, externalRef));
        }

        var error = "Simulated gateway rejection (5% failure rate)";
        _logger.LogWarning(
            "[FakeGateway] Withdrawal FAILED | Wallet: {WalletId} | Amount: {Amount} {Currency} | Reason: {Reason}",
            walletId, amount, currency, error);

        return Task.FromResult(new PaymentResult(false, externalRef, error));
    }

    /// <inheritdoc />
    /// <remarks>Refunds always succeed in this simulation.</remarks>
    public Task<PaymentResult> RefundAsync(
        string externalRef, decimal amount, string currency, CancellationToken ct = default)
    {
        var newRef = GenerateRef();
        _logger.LogInformation(
            "[FakeGateway] Refund SUCCESS | OriginalRef: {OriginalRef} | Amount: {Amount} {Currency} | NewRef: {NewRef}",
            externalRef, amount, currency, newRef);

        return Task.FromResult(new PaymentResult(true, newRef));
    }

    private static string GenerateRef() => $"FAKE-{Guid.NewGuid():N}".ToUpperInvariant();
}
