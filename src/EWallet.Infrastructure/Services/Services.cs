using System.Net;
using System.Security.Claims;
using EWallet.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EWallet.Infrastructure.Services;

/// <summary>
/// BCrypt-based password hasher.
/// Work factor is set to 12 to balance security and latency on modern hardware.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    /// <inheritdoc />
    public bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}

/// <summary>
/// Simulated notification service that logs email/SMS sends at Debug level.
/// Replace with a real provider (SendGrid, Twilio, etc.) before production.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    /// <summary>Initializes the notification service.</summary>
    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        // SECURITY: never log sensitive values such as OTP codes from the body in production.
        _logger.LogDebug(
            "[SIMULATED EMAIL] To: {To} | Subject: {Subject} | BodyLength: {BodyLength}",
            to, subject, body.Length);

        await Task.Delay(50, ct); // Simulate async I/O
    }

    /// <inheritdoc />
    public async Task SendSmsAsync(string to, string message, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[SIMULATED SMS] To: {To} | MessageLength: {MessageLength}",
            to, message.Length);

        await Task.Delay(50, ct);
    }
}

/// <summary>
/// Extracts the authenticated user's identity and IP address from the current HTTP context.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Initializes the service with the HTTP context accessor.</summary>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?
                .User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public string IpAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null) return "unknown";

            // Respect reverse-proxy header if present
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
