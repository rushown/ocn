using EWallet.Application.Common.Interfaces;
using EWallet.Application.Transactions.DTOs;
using EWallet.API.Hubs;
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
    public async Task NotifyBalanceUpdatedAsync(Guid userId, decimal newBalance, CancellationToken ct = default)
    {
        _logger.LogDebug("Notifying user {UserId} of balance update: {NewBalance}", userId, newBalance);

        await _hubContext.Clients
            .Group($"user_{userId}")
            .SendAsync("BalanceUpdated", new { userId, newBalance, timestamp = DateTime.UtcNow }, ct);
    }

    /// <summary>
    /// Notifies the user that a transaction's status has changed.
    /// SignalR event name: "TransactionUpdated"
    /// </summary>
    public async Task NotifyTransactionStatusChangedAsync(
        Guid userId,
        TransactionDto transaction,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Notifying user {UserId} of transaction status change: {TransactionId} → {Status}",
            userId, transaction.Id, transaction.Status);

        await _hubContext.Clients
            .Group($"user_{userId}")
            .SendAsync("TransactionUpdated", transaction, ct);
    }
}
