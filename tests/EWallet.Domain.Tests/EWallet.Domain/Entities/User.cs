using EWallet.Domain.Abstractions;
using EWallet.Domain.Enums;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;

namespace EWallet.Domain.Entities;

public sealed class User : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public KycLevel KycLevel { get; private set; }
    public bool IsTwoFactorEnabled { get; private set; }
    public string? TotpSecret { get; private set; }

    private User() { }

    public static User Create(string email, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email cannot be empty.");

        var normalized = email.Trim().ToLowerInvariant();

        // Basic format guard — real implementations may use a more thorough check
        if (!normalized.Contains('@') || !normalized.Contains('.'))
            throw new DomainException($"'{email}' is not a valid email address.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            FirstName = firstName,
            LastName = lastName,
            KycLevel = KycLevel.Unverified,
            IsTwoFactorEnabled = false
        };
    }

    public void UpgradeKyc(KycLevel newLevel)
    {
        if (newLevel <= KycLevel)
            throw new DomainException(
                $"Cannot downgrade or re-set KYC from {KycLevel} to {newLevel}.");

        var old = KycLevel;
        KycLevel = newLevel;
        RaiseDomainEvent(new UserKycUpgradedEvent(Id, old, KycLevel));
    }

    public void EnableTwoFactor(string totpSecret)
    {
        if (string.IsNullOrWhiteSpace(totpSecret))
            throw new DomainException("TOTP secret cannot be empty.");

        if (IsTwoFactorEnabled)
            throw new DomainException("Two-factor authentication is already enabled.");

        TotpSecret = totpSecret;
        IsTwoFactorEnabled = true;
    }
}
