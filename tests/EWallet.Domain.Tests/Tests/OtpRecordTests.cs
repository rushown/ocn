using FluentAssertions;
using EWallet.Domain.Entities;
using EWallet.Domain.Exceptions;

namespace EWallet.Domain.Tests.Tests;

public class OtpRecordTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static OtpRecord CreateFreshOtp(TimeSpan? ttl = null) =>
        OtpRecord.Create(
            userId:    Guid.NewGuid(),
            code:      "123456",
            expiresAt: DateTime.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(5)));

    private static OtpRecord CreateExpiredOtp() =>
        OtpRecord.Create(
            userId:    Guid.NewGuid(),
            code:      "654321",
            expiresAt: DateTime.UtcNow.AddMinutes(-1)); // already in the past

    // ─── MarkUsed – valid unused ──────────────────────────────────────────────

    [Fact]
    public void MarkUsed_FreshUnusedOtp_SetsIsUsedTrue()
    {
        var otp = CreateFreshOtp();

        otp.MarkUsed();

        otp.IsUsed.Should().BeTrue();
    }

    [Fact]
    public void MarkUsed_FreshUnusedOtp_RecordsUsedAtTimestamp()
    {
        var otp = CreateFreshOtp();

        otp.MarkUsed();

        otp.UsedAt.Should().NotBeNull();
        otp.UsedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    // ─── MarkUsed – already used ─────────────────────────────────────────────

    [Fact]
    public void MarkUsed_AlreadyUsedOtp_ThrowsOtpAlreadyUsedException()
    {
        var otp = CreateFreshOtp();
        otp.MarkUsed(); // first use — valid

        var act = () => otp.MarkUsed(); // second use — must throw

        act.Should().Throw<OtpAlreadyUsedException>(
            because: "an OTP is a one-time code and may not be consumed twice");
    }

    // ─── MarkUsed – expired ───────────────────────────────────────────────────

    [Fact]
    public void MarkUsed_ExpiredOtp_ThrowsOtpExpiredException()
    {
        var otp = CreateExpiredOtp();

        var act = () => otp.MarkUsed();

        act.Should().Throw<OtpExpiredException>(
            because: "an OTP past its expiry time must be rejected regardless of used status");
    }

    [Fact]
    public void MarkUsed_OtpExpiresInFuture_DoesNotThrow()
    {
        var otp = CreateFreshOtp(ttl: TimeSpan.FromMinutes(10));

        var act = () => otp.MarkUsed();

        act.Should().NotThrow();
    }

    // ─── Expiry boundary ─────────────────────────────────────────────────────

    [Fact]
    public void IsExpired_FutureExpiresAt_ReturnsFalse()
    {
        var otp = CreateFreshOtp();

        otp.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_PastExpiresAt_ReturnsTrue()
    {
        var otp = CreateExpiredOtp();

        otp.IsExpired.Should().BeTrue();
    }
}
