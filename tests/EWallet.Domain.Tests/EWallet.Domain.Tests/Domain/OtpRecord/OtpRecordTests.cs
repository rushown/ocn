namespace EWallet.Domain.Tests.Domain.OtpRecord;

public sealed class OtpRecordTests
{
    // -----------------------------------------------------------------------
    // OtpRecord.MarkUsed — valid, unused, not expired
    // -----------------------------------------------------------------------

    [Fact]
    public void MarkUsed_ValidUnusedNotExpired_SetsIsUsedTrue()
    {
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));

        otp.MarkUsed();

        otp.IsUsed.Should().BeTrue();
    }

    [Fact]
    public void MarkUsed_ValidUnusedNotExpired_DoesNotThrow()
    {
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));

        var act = () => otp.MarkUsed();

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // OtpRecord.MarkUsed — already used
    // -----------------------------------------------------------------------

    [Fact]
    public void MarkUsed_AlreadyUsed_ThrowsOtpAlreadyUsedException()
    {
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));
        otp.MarkUsed(); // use once

        var act = () => otp.MarkUsed(); // use again

        act.Should().Throw<OtpAlreadyUsedException>();
    }

    // -----------------------------------------------------------------------
    // OtpRecord.MarkUsed — expired
    // -----------------------------------------------------------------------

    [Fact]
    public void MarkUsed_Expired_ThrowsOtpExpiredException()
    {
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var act = () => otp.MarkUsed();

        act.Should().Throw<OtpExpiredException>();
    }

    [Fact]
    public void MarkUsed_ExpiresAtExactlyNow_ThrowsOtpExpiredException()
    {
        // Boundary: expiry equal to (or just before) now is considered expired
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));

        var act = () => otp.MarkUsed();

        act.Should().Throw<OtpExpiredException>();
    }

    // -----------------------------------------------------------------------
    // IsExpired property
    // -----------------------------------------------------------------------

    [Fact]
    public void IsExpired_WhenExpiresAtInFuture_ReturnsFalse()
    {
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddMinutes(10));
        otp.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-10));
        otp.IsExpired.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Creation
    // -----------------------------------------------------------------------

    [Fact]
    public void NewOtpRecord_IsUsedFalse()
    {
        var otp = CreateOtp(expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));
        otp.IsUsed.Should().BeFalse();
    }

    [Fact]
    public void NewOtpRecord_SetsCodeAndOwner()
    {
        var userId = Guid.NewGuid();
        var otp = EWallet.Domain.Entities.OtpRecord.Create(userId, "123456", DateTimeOffset.UtcNow.AddMinutes(5));

        otp.UserId.Should().Be(userId);
        otp.Code.Should().Be("123456");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static EWallet.Domain.Entities.OtpRecord CreateOtp(DateTimeOffset expiresAt)
        => EWallet.Domain.Entities.OtpRecord.Create(Guid.NewGuid(), "999999", expiresAt);
}
