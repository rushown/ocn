using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EWallet.API.Hubs;

/// <summary>
/// SignalR hub for real-time wallet notifications.
/// Clients connect and are automatically placed in a per-user group.
/// JWT is supplied via query-string ?access_token= for WebSocket negotiation.
/// </summary>
[Authorize]
public class WalletHub : Hub
{
    private readonly ILogger<WalletHub> _logger;

    public WalletHub(ILogger<WalletHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation(
                "User {UserId} connected to WalletHub (ConnectionId: {ConnectionId})",
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation(
                "User {UserId} disconnected from WalletHub (ConnectionId: {ConnectionId})",
                userId, Context.ConnectionId);
        }

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "WalletHub disconnected with error for ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
