using EWallet.Domain.ValueObjects;

namespace EWallet.Application.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessDepositAsync(Guid walletId, Money amount, string externalRef, CancellationToken ct);
    Task<PaymentResult> ProcessWithdrawalAsync(Guid walletId, Money amount, string externalRef, CancellationToken ct);
    Task<PaymentResult> RefundAsync(string originalTransactionRef, Money amount, CancellationToken ct);
}

public record PaymentResult(bool IsSuccess, string? TransactionRef, string? ErrorMessage);
