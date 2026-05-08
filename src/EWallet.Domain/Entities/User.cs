using EWallet.Domain.Enums;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;

namespace EWallet.Domain.Entities;

/// <summary>
/// Represents a registered user in the e-wallet system.
/// All state mutations are performed through explicit methods; no public setters are exposed.
/// </summary>
public sealed class User : BaseEntity
{
    /// <summary>The user's email address (lowercase, trimmed).</summary>
    public string Email { get; private set; } = default!;

    /// <summary>The user's phone number.</summary>
    public string PhoneNumber { get; private set; } = default!;

    /// <summary>The user's display name.</summary>
    public string FullName { get; private set; } = default!;

    /// <summary>Bcrypt / Argon2 hash of the user's password. Never store plain-text.</summary>
    public string PasswordHash { get; private set; } = default!;

    /// <summary>Current KYC verification tier, controlling daily transaction limits.</summary>
    public KycLevel KycLevel { get; private set; } = KycLevel.Unverified;

    /// <summary>Whether two-factor authentication is currently active for this user.</summary>
    public bool IsTwoFactorEnabled { get; private set; }

    /// <summary>TOTP shared secret used to generate/validate 2FA codes. Null when 2FA is disabled.</summary>
    public string? TwoFactorSecret { get; private set; }

    /// <summary>Whether the account is active and permitted to operate.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Current JWT refresh token issued to the user, or <c>null</c> if not logged in.</summary>
    public string? RefreshToken { get; private set; }

    /// <summary>UTC expiry of the current refresh token, or <c>null</c> if none issued.</summary>
    public DateTime? RefreshTokenExpiry { get; private set; }

    // EF Core parameterless constructor
    private User() { }

    /// <summary>
    /// Creates a new, unverified <see cref="User"/>.
    /// </summary>
    /// <param name="email">Email address — stored lowercase and trimmed.</param>
    /// <param name="phone">Phone number in E.164 or local format.</param>
    /// <param name="fullName">Display name.</param>
    /// <param name="passwordHash">Pre-computed password hash (never pass plain-text).</param>
    /// <returns>A new <see cref="User"/> with <see cref="KycLevel.Unverified"/>.</returns>
    /// <exception cref="DomainException">Thrown when any required argument is null or whitespace.</exception>
    public static User Create(string email, string phone, string fullName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email is required");
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Phone number is required");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Full name is required");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required");

        return new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phone.Trim(),
            FullName = fullName.Trim(),
            PasswordHash = passwordHash
        };
    }

    /// <summary>
    /// Upgrades the user's KYC tier. The new level must be strictly higher than the current one.
    /// Raises <see cref="UserKycUpgradedEvent"/>.
    /// </summary>
    /// <param name="newLevel">The target KYC tier.</param>
    /// <exception cref="DomainException">Thrown when <paramref name="newLevel"/> is not a promotion.</exception>
    public void UpgradeKyc(KycLevel newLevel)
    {
        if (newLevel <= KycLevel)
            throw new DomainException(
                $"New KYC level '{newLevel}' must be higher than current level '{KycLevel}'");

        var oldLevel = KycLevel;
        KycLevel = newLevel;
        SetUpdated();
        AddDomainEvent(new UserKycUpgradedEvent(Id, oldLevel, newLevel));
    }

    /// <summary>
    /// Enables two-factor authentication for this user and stores the TOTP secret.
    /// </summary>
    /// <param name="secret">Base32-encoded TOTP shared secret.</param>
    /// <exception cref="DomainException">Thrown when <paramref name="secret"/> is null or whitespace.</exception>
    public void EnableTwoFactor(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new DomainException("Two-factor secret is required");

        TwoFactorSecret = secret;
        IsTwoFactorEnabled = true;
        SetUpdated();
    }

    /// <summary>Disables two-factor authentication and clears the stored TOTP secret.</summary>
    public void DisableTwoFactor()
    {
        TwoFactorSecret = null;
        IsTwoFactorEnabled = false;
        SetUpdated();
    }

    /// <summary>
    /// Stores a new refresh token and its expiry, replacing any previously issued token.
    /// </summary>
    /// <param name="token">The opaque refresh token string.</param>
    /// <param name="expiry">UTC datetime when the token expires.</param>
    public void UpdateRefreshToken(string token, DateTime expiry)
    {
        RefreshToken = token;
        RefreshTokenExpiry = expiry;
        SetUpdated();
    }

    /// <summary>Deactivates the account, preventing further transactions or logins.</summary>
    public void Deactivate()
    {
        IsActive = false;
        RefreshToken = null;
        RefreshTokenExpiry = null;
        SetUpdated();
    }

    /// <summary>Clears the current refresh token, effectively logging the user out of all sessions.</summary>
    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiry = null;
        SetUpdated();
    }

    /// <summary>Replaces the stored password hash (e.g. after a password change).</summary>
    /// <param name="hash">New pre-computed password hash.</param>
    /// <exception cref="DomainException">Thrown when <paramref name="hash"/> is null or whitespace.</exception>
    public void SetPasswordHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new DomainException("Password hash is required");

        PasswordHash = hash;
        SetUpdated();
    }

    /// <summary>Updates the user's display name and phone number.</summary>
    /// <param name="fullName">New display name.</param>
    /// <param name="phoneNumber">New phone number.</param>
    public void UpdateProfile(string fullName, string phoneNumber)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
            FullName = fullName.Trim();
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber.Trim();
        SetUpdated();
    }

    /// <summary>
    /// Generates a new TOTP secret, enables two-factor authentication, and returns the secret
    /// so the caller can render a QR code for the authenticator app.
    /// </summary>
    /// <returns>A Base32-encoded TOTP shared secret.</returns>
    public string GenerateTotpSecret()
    {
        var bytes = new byte[20];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < bytes.Length; i += 5)
        {
            int take = Math.Min(5, bytes.Length - i);
            long buf = 0;
            for (int j = 0; j < take; j++) buf = (buf << 8) | bytes[i + j];
            int bits = take * 8;
            while (bits > 0) { bits -= 5; sb.Append(alphabet[(int)((buf >> Math.Max(bits, 0)) & 0x1F)]); }
        }
        var secret = sb.ToString();
        EnableTwoFactor(secret);
        return secret;
    }
}
