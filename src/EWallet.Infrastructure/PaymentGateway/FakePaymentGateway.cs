using EWallet.Application.Interfaces;
using EWallet.Domain.ValueObjects;
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
        Guid walletId, Money amount, string externalRef, CancellationToken ct = default)
    {
        var providerRef = GenerateRef();
        _logger.LogInformation(
            "[FakeGateway] Deposit SUCCESS | Wallet: {WalletId} | Amount: {Amount} {Currency} | Ref: {Ref}",
            walletId, amount.Amount, amount.Currency, providerRef);

        return Task.FromResult(new PaymentResult(true, providerRef, null));
    }

    /// <inheritdoc />
    /// <remarks>Withdrawals succeed with a 95% probability.</remarks>
    public Task<PaymentResult> ProcessWithdrawalAsync(
        Guid walletId, Money amount, string externalRef, CancellationToken ct = default)
    {
        var success = Random.Shared.NextDouble() < 0.95;
        var providerRef = GenerateRef();

        if (success)
        {
            _logger.LogInformation(
                "[FakeGateway] Withdrawal SUCCESS | Wallet: {WalletId} | Amount: {Amount} {Currency} | Ref: {Ref}",
                walletId, amount.Amount, amount.Currency, providerRef);

            return Task.FromResult(new PaymentResult(true, providerRef, null));
        }

        var error = "Simulated gateway rejection (5% failure rate)";
        _logger.LogWarning(
            "[FakeGateway] Withdrawal FAILED | Wallet: {WalletId} | Amount: {Amount} {Currency} | Reason: {Reason}",
            walletId, amount.Amount, amount.Currency, error);

        return Task.FromResult(new PaymentResult(false, providerRef, error));
    }

    /// <inheritdoc />
    /// <remarks>Refunds always succeed in this simulation.</remarks>
    public Task<PaymentResult> RefundAsync(
        string originalTransactionRef, Money amount, CancellationToken ct = default)
    {
        var newRef = GenerateRef();
        _logger.LogInformation(
            "[FakeGateway] Refund SUCCESS | OriginalRef: {OriginalRef} | Amount: {Amount} {Currency} | NewRef: {NewRef}",
            originalTransactionRef, amount.Amount, amount.Currency, newRef);

        return Task.FromResult(new PaymentResult(true, newRef, null));
    }

    private static string GenerateRef() => $"FAKE-{Guid.NewGuid():N}".ToUpperInvariant();
}
