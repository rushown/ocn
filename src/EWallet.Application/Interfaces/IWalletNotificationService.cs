using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;

namespace EWallet.Application.Interfaces;

/// <summary>
/// SignalR real-time notification abstraction for wallet events.
/// </summary>
public interface IWalletNotificationService
{
    Task NotifyBalanceUpdatedAsync(Guid userId, Money newBalance, CancellationToken ct);
    Task NotifyTransactionStatusChangedAsync(Guid userId, Guid transactionId, TransactionStatus status, CancellationToken ct);
}
