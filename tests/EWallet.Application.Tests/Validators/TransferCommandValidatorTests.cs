using FluentAssertions;
using FluentValidation.TestHelper;
using EWallet.Application.Commands.Transfer;
using EWallet.Application.Validators;

namespace EWallet.Application.Tests.Validators;

public class TransferCommandValidatorTests
{
    private readonly TransferCommandValidator _validator = new();

    // ─── Amount boundaries ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(100_001)]
    public void Amount_OutOfRange_ShouldFail(decimal amount)
    {
        var command = new TransferCommand { Amount = amount, IdempotencyKey = "valid-key-xyz" };

        _validator.TestValidate(command)
                  .ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(100_000)]
    public void Amount_InRange_ShouldPass(decimal amount)
    {
        var command = new TransferCommand
        {
            Amount         = amount,
            IdempotencyKey = "valid-key-xyz",
            OtpCode        = amount > 500 ? "123456" : null, // supply OTP where required
        };

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    // ─── OTP conditional requirement ─────────────────────────────────────────

    [Fact]
    public void OtpCode_Required_WhenAmountAbove500_AndMissing_ShouldFail()
    {
        var command = new TransferCommand
        {
            Amount         = 600m,
            IdempotencyKey = "valid-key-xyz",
            OtpCode        = null, // deliberately absent
        };

        _validator.TestValidate(command)
                  .ShouldHaveValidationErrorFor(x => x.OtpCode);
    }

    [Fact]
    public void OtpCode_NotRequired_WhenAmountAtOrBelow500_ShouldPass()
    {
        var command = new TransferCommand
        {
            Amount         = 499.99m,
            IdempotencyKey = "valid-key-xyz",
            OtpCode        = null,
        };

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.OtpCode);
    }

    [Fact]
    public void OtpCode_ProvidedForLargeAmount_ShouldPass()
    {
        var command = new TransferCommand
        {
            Amount         = 750m,
            IdempotencyKey = "valid-key-xyz",
            OtpCode        = "654321",
        };

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.OtpCode);
    }

    // ─── Idempotency key ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ab")]   // shorter than minimum 3 chars
    public void IdempotencyKey_Invalid_ShouldFail(string? key)
    {
        var command = new TransferCommand { Amount = 100m, IdempotencyKey = key! };

        _validator.TestValidate(command)
                  .ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_Valid_ShouldPass()
    {
        var command = new TransferCommand
        {
            Amount         = 100m,
            IdempotencyKey = "abc", // minimum valid length
        };

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.IdempotencyKey);
    }
}
