using EWallet.Domain.Enums;
using EWallet.Domain.Exceptions;

namespace EWallet.Domain.Entities;

/// <summary>
/// Represents a one-time password (OTP) code issued to a user for a specific purpose.
/// A code may only be consumed once and cannot be used after it expires.
/// </summary>
public sealed class OtpRecord : BaseEntity
{
    /// <summary>The user this OTP was issued to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>The 6-digit OTP code.</summary>
    public string Code { get; private set; } = default!;

    /// <summary>The action this OTP authorises.</summary>
    public OtpPurpose Purpose { get; private set; }

    /// <summary>UTC timestamp after which the code is no longer valid.</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Whether the code has already been consumed by a successful verification.</summary>
    public bool IsUsed { get; private set; }

    // EF Core parameterless constructor
    private OtpRecord() { }

    /// <summary>
    /// Issues a new OTP for <paramref name="userId"/>.
    /// The 6-digit code is generated using a cryptographically random source.
    /// </summary>
    /// <param name="userId">The target user.</param>
    /// <param name="purpose">The action the OTP will authorise.</param>
    /// <param name="expiryMinutes">How many minutes until the code expires. Defaults to 5.</param>
    /// <returns>A new, unused <see cref="OtpRecord"/>.</returns>
    public static OtpRecord Create(Guid userId, OtpPurpose purpose, int expiryMinutes = 5)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId cannot be empty");
        if (expiryMinutes <= 0)
            throw new DomainException("Expiry minutes must be positive");

        return new OtpRecord
        {
            UserId = userId,
            Code = GenerateCode(),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            IsUsed = false
        };
    }

    /// <summary>
    /// Marks this OTP as consumed, preventing re-use.
    /// </summary>
    /// <exception cref="OtpAlreadyUsedException">Thrown when the code has already been consumed.</exception>
    /// <exception cref="OtpExpiredException">Thrown when the code has passed its expiry time.</exception>
    public void MarkUsed()
    {
        if (IsUsed)
            throw new OtpAlreadyUsedException();
        if (DateTime.UtcNow > ExpiresAt)
            throw new OtpExpiredException();

        IsUsed = true;
    }

    private static string GenerateCode()
    {
        // Cryptographically random 6-digit code (000000–999999)
        var bytes = new byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1_000_000;
        return value.ToString("D6");
    }
}
