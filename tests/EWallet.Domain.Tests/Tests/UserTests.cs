using FluentAssertions;
using EWallet.Domain.Entities;
using EWallet.Domain.Enums;
using EWallet.Domain.Events;
using EWallet.Domain.Exceptions;

namespace EWallet.Domain.Tests.Tests;

public class UserTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static User CreateUnverifiedUser() =>
        User.Create(
            email:       "user@ewallet.test",
            passwordHash: "hashed-pw",
            fullName:    "Test User",
            phoneNumber: "+14155552671");

    private static User CreateUserAtTier(UserTier tier)
    {
        var user = CreateUnverifiedUser();
        if (tier >= UserTier.Tier1) user.UpgradeKyc(UserTier.Tier1);
        if (tier >= UserTier.Tier2) user.UpgradeKyc(UserTier.Tier2);
        user.ClearDomainEvents();
        return user;
    }

    // ─── User.Create – email normalisation ──────────────────────────────────

    [Fact]
    public void Create_EmailWithLeadingTrailingSpacesAndUpperCase_NormalisesToLowerTrimmed()
    {
        var user = User.Create(
            email:        " TEST@EXAMPLE.COM ",
            passwordHash: "hash",
            fullName:     "Test User",
            phoneNumber:  "+14155552671");

        user.Email.Should().Be("test@example.com",
            because: "emails are case-insensitive and must be stored normalised");
    }

    [Fact]
    public void Create_ValidData_SetsIsActiveTrue()
    {
        var user = CreateUnverifiedUser();

        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ValidData_SetsKycLevelToUnverified()
    {
        var user = CreateUnverifiedUser();

        user.KycLevel.Should().Be(UserTier.Unverified);
    }

    // ─── UpgradeKyc – valid upgrade ──────────────────────────────────────────

    [Fact]
    public void UpgradeKyc_UnverifiedToTier1_SetsKycLevelAndRaisesUserKycUpgradedEvent()
    {
        var user = CreateUnverifiedUser();

        user.UpgradeKyc(UserTier.Tier1);

        user.KycLevel.Should().Be(UserTier.Tier1);
        user.DomainEvents.Should().ContainSingle(e => e is UserKycUpgradedEvent,
            because: "a KYC level change must publish a domain event for audit purposes");
    }

    [Fact]
    public void UpgradeKyc_Tier1ToTier2_SetsKycLevelAndRaisesEvent()
    {
        var user = CreateUserAtTier(UserTier.Tier1);

        user.UpgradeKyc(UserTier.Tier2);

        user.KycLevel.Should().Be(UserTier.Tier2);
        user.DomainEvents.Should().ContainSingle(e => e is UserKycUpgradedEvent);
    }

    // ─── UpgradeKyc – downgrade blocked ──────────────────────────────────────

    [Fact]
    public void UpgradeKyc_Tier2ToTier1_ThrowsDomainException()
    {
        var user = CreateUserAtTier(UserTier.Tier2);

        var act = () => user.UpgradeKyc(UserTier.Tier1);

        act.Should().Throw<DomainException>(
            because: "KYC level can only move forward, never backwards");
    }

    [Fact]
    public void UpgradeKyc_SameLevel_ThrowsDomainException()
    {
        var user = CreateUserAtTier(UserTier.Tier1);

        var act = () => user.UpgradeKyc(UserTier.Tier1);

        act.Should().Throw<DomainException>(
            because: "re-setting the same KYC level is not a valid upgrade");
    }

    // ─── EnableTwoFactor ─────────────────────────────────────────────────────

    [Fact]
    public void EnableTwoFactor_ValidSecret_SetsTwoFactorEnabledTrue()
    {
        var user = CreateUnverifiedUser();

        user.EnableTwoFactor("TOTP_SECRET_BASE32");

        user.IsTwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public void EnableTwoFactor_ValidSecret_PersistsTotpSecret()
    {
        var user = CreateUnverifiedUser();

        user.EnableTwoFactor("TOTP_SECRET_BASE32");

        user.TotpSecret.Should().Be("TOTP_SECRET_BASE32");
    }

    [Fact]
    public void EnableTwoFactor_NullOrEmptySecret_ThrowsDomainException()
    {
        var user = CreateUnverifiedUser();

        var act = () => user.EnableTwoFactor(string.Empty);

        act.Should().Throw<DomainException>(
            because: "a blank TOTP secret is not a valid 2FA configuration");
    }

    // ─── DisableTwoFactor ────────────────────────────────────────────────────

    [Fact]
    public void DisableTwoFactor_WhenEnabled_SetsTwoFactorEnabledFalse()
    {
        var user = CreateUnverifiedUser();
        user.EnableTwoFactor("SECRET");
        user.ClearDomainEvents();

        user.DisableTwoFactor();

        user.IsTwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public void DisableTwoFactor_WhenNotEnabled_ThrowsDomainException()
    {
        var user = CreateUnverifiedUser(); // 2FA not enabled

        var act = () => user.DisableTwoFactor();

        act.Should().Throw<DomainException>();
    }

    // ─── Deactivate ──────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ActiveUser_SetsIsActiveFalse()
    {
        var user = CreateUnverifiedUser();

        user.Deactivate();

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_AlreadyInactiveUser_ThrowsDomainException()
    {
        var user = CreateUnverifiedUser();
        user.Deactivate();

        var act = () => user.Deactivate();

        act.Should().Throw<DomainException>();
    }
}
