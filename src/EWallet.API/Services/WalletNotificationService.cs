using EWallet.Application.Interfaces;
using EWallet.API.Hubs;
using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace EWallet.API.Services;

/// <summary>
/// Pushes real-time wallet events to connected SignalR clients.
/// Each user is in the group "user_{userId}" — messages are targeted per-user.
/// </summary>
public class WalletNotificationService : IWalletNotificationService
{
    private readonly IHubContext<WalletHub> _hubContext;
    private readonly ILogger<WalletNotificationService> _logger;

    public WalletNotificationService(
        IHubContext<WalletHub> hubContext,
        ILogger<WalletNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Notifies the user that their wallet balance has changed.
    /// SignalR event name: "BalanceUpdated"
    /// </summary>
    public async Task NotifyBalanceUpdatedAsync(Guid userId, Money newBalance, CancellationToken ct)
    {
        _logger.LogDebug("Notifying user {UserId} of balance update: {NewBalance}", userId, newBalance.Amount);

        await _hubContext.Clients
            .Group($"user_{userId}")
            .SendAsync("BalanceUpdated", new { userId, newBalance = newBalance.Amount, currency = newBalance.Currency, timestamp = DateTime.UtcNow }, ct);
    }

    /// <summary>
    /// Notifies the user that a transaction's status has changed.
    /// SignalR event name: "TransactionUpdated"
    /// </summary>
    public async Task NotifyTransactionStatusChangedAsync(Guid userId, Guid transactionId, TransactionStatus status, CancellationToken ct)
    {
        _logger.LogDebug("Notifying user {UserId} of transaction status change: {TransactionId} → {Status}", userId, transactionId, status);

        await _hubContext.Clients
            .Group($"user_{userId}")
            .SendAsync("TransactionUpdated", new { transactionId, status = status.ToString() }, ct);
    }
}
