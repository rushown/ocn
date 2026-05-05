using System.Security.Claims;

namespace EWallet.Infrastructure.Interfaces;

// ──────────────────────────────────────────────────────────────────
// These interfaces are declared here so the Infrastructure project
// compiles standalone. In a real solution they live in EWallet.Application.
// ──────────────────────────────────────────────────────────────────

/// <summary>Generates and validates JWT access tokens and opaque refresh tokens.</summary>
public interface IJwtService
{
    /// <summary>Generates a short-lived JWT access token for the given user claims.</summary>
    string GenerateAccessToken(Guid userId, string email, int kycLevel);

    /// <summary>Generates a cryptographically random opaque refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>Validates a JWT token and returns the <see cref="ClaimsPrincipal"/> on success, or <c>null</c> on failure.</summary>
    ClaimsPrincipal? ValidateToken(string token);
}

/// <summary>Hashes and verifies passwords using a strong KDF.</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plain-text password.</summary>
    string Hash(string password);

    /// <summary>Verifies a plain-text password against a stored hash.</summary>
    bool Verify(string password, string hash);
}

/// <summary>Sends email and SMS notifications.</summary>
public interface INotificationService
{
    /// <summary>Sends an email notification.</summary>
    Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default);

    /// <summary>Sends an SMS notification.</summary>
    Task SendSmsAsync(string to, string message, CancellationToken ct = default);
}

/// <summary>Provides information about the currently authenticated HTTP request.</summary>
public interface ICurrentUserService
{
    /// <summary>The authenticated user's ID, or <c>null</c> for anonymous requests.</summary>
    Guid? UserId { get; }

    /// <summary>The client's remote IP address.</summary>
    string IpAddress { get; }
}

/// <summary>Cache abstraction over Redis (or any backing store).</summary>
public interface ICacheService
{
    /// <summary>Returns the cached value for <paramref name="key"/>, or <c>null</c> on miss.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/> with the given TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Removes the entry for <paramref name="key"/>.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
}

/// <summary>Prevents duplicate processing of client requests using an idempotency key.</summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to store <paramref name="response"/> for <paramref name="idempotencyKey"/> using SET NX.
    /// Returns <c>true</c> if this is the first time the key was seen (caller should process the request).
    /// Returns <c>false</c> if the key already exists (caller should return the cached response).
    /// </summary>
    Task<bool> TrySetAsync<T>(string idempotencyKey, T response, CancellationToken ct = default);

    /// <summary>Returns the previously stored response for <paramref name="idempotencyKey"/>, or <c>null</c>.</summary>
    Task<T?> GetAsync<T>(string idempotencyKey, CancellationToken ct = default);
}

/// <summary>Result returned by the payment gateway.</summary>
public sealed record PaymentResult(bool IsSuccess, string ExternalRef, string? ErrorMessage = null);

/// <summary>External payment gateway abstraction.</summary>
public interface IPaymentGateway
{
    /// <summary>Processes a deposit from an external source into the wallet.</summary>
    Task<PaymentResult> ProcessDepositAsync(Guid walletId, decimal amount, string currency, CancellationToken ct = default);

    /// <summary>Processes a withdrawal from the wallet to an external destination.</summary>
    Task<PaymentResult> ProcessWithdrawalAsync(Guid walletId, decimal amount, string currency, CancellationToken ct = default);

    /// <summary>Refunds a previously processed transaction.</summary>
    Task<PaymentResult> RefundAsync(string externalRef, decimal amount, string currency, CancellationToken ct = default);
}
