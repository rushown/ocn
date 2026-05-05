namespace EWallet.Domain.Tests.Domain.User;

public sealed class UserTests
{
    // -----------------------------------------------------------------------
    // User.Create
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_EmailNormalized_TrimmedAndLowercased()
    {
        var user = EWallet.Domain.Entities.User.Create(" TEST@EXAMPLE.COM ", "First", "Last");

        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Create_InitialKycLevel_IsUnverified()
    {
        var user = EWallet.Domain.Entities.User.Create("user@example.com", "First", "Last");

        user.KycLevel.Should().Be(KycLevel.Unverified);
    }

    [Fact]
    public void Create_IsTwoFactorEnabled_IsFalse()
    {
        var user = EWallet.Domain.Entities.User.Create("user@example.com", "First", "Last");

        user.IsTwoFactorEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Create_InvalidEmail_ThrowsDomainException(string email)
    {
        var act = () => EWallet.Domain.Entities.User.Create(email, "First", "Last");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_NullEmail_ThrowsDomainException()
    {
        var act = () => EWallet.Domain.Entities.User.Create(null!, "First", "Last");

        act.Should().Throw<DomainException>();
    }

    // -----------------------------------------------------------------------
    // User.UpgradeKyc — valid path
    // -----------------------------------------------------------------------

    [Fact]
    public void UpgradeKyc_UnverifiedToTier1_SetsKycLevel()
    {
        var user = CreateUnverifiedUser();

        user.UpgradeKyc(KycLevel.Tier1);

        user.KycLevel.Should().Be(KycLevel.Tier1);
    }

    [Fact]
    public void UpgradeKyc_UnverifiedToTier1_RaisesUserKycUpgradedEvent()
    {
        var user = CreateUnverifiedUser();

        user.UpgradeKyc(KycLevel.Tier1);

        user.DomainEvents.Should().ContainSingle(e => e is UserKycUpgradedEvent);
    }

    [Fact]
    public void UpgradeKyc_Tier1ToTier2_Succeeds()
    {
        var user = CreateUnverifiedUser();
        user.UpgradeKyc(KycLevel.Tier1);
        user.ClearDomainEvents();

        user.UpgradeKyc(KycLevel.Tier2);

        user.KycLevel.Should().Be(KycLevel.Tier2);
        user.DomainEvents.Should().ContainSingle(e => e is UserKycUpgradedEvent);
    }

    // -----------------------------------------------------------------------
    // User.UpgradeKyc — downgrade not allowed
    // -----------------------------------------------------------------------

    [Fact]
    public void UpgradeKyc_DowngradeFromTier2ToTier1_ThrowsDomainException()
    {
        var user = CreateUnverifiedUser();
        user.UpgradeKyc(KycLevel.Tier1);
        user.UpgradeKyc(KycLevel.Tier2);

        var act = () => user.UpgradeKyc(KycLevel.Tier1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpgradeKyc_SameLevel_ThrowsDomainException()
    {
        var user = CreateUnverifiedUser();
        user.UpgradeKyc(KycLevel.Tier1);

        var act = () => user.UpgradeKyc(KycLevel.Tier1);

        act.Should().Throw<DomainException>();
    }

    // -----------------------------------------------------------------------
    // User.EnableTwoFactor
    // -----------------------------------------------------------------------

    [Fact]
    public void EnableTwoFactor_SetsIsTwoFactorEnabledTrue()
    {
        var user = CreateUnverifiedUser();

        user.EnableTwoFactor("TOTP_SECRET");

        user.IsTwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public void EnableTwoFactor_StoresTotpSecret()
    {
        var user = CreateUnverifiedUser();

        user.EnableTwoFactor("TOTP_SECRET");

        user.TotpSecret.Should().Be("TOTP_SECRET");
    }

    [Fact]
    public void EnableTwoFactor_AlreadyEnabled_ThrowsDomainException()
    {
        var user = CreateUnverifiedUser();
        user.EnableTwoFactor("SECRET_1");

        var act = () => user.EnableTwoFactor("SECRET_2");

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EnableTwoFactor_EmptySecret_ThrowsDomainException(string secret)
    {
        var user = CreateUnverifiedUser();

        var act = () => user.EnableTwoFactor(secret);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void EnableTwoFactor_NullSecret_ThrowsDomainException()
    {
        var user = CreateUnverifiedUser();

        var act = () => user.EnableTwoFactor(null!);

        act.Should().Throw<DomainException>();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static EWallet.Domain.Entities.User CreateUnverifiedUser()
        => EWallet.Domain.Entities.User.Create("user@example.com", "Jane", "Doe");
}
