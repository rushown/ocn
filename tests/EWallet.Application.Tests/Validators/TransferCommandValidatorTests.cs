using FluentAssertions;
using EWallet.Application.Commands;
using EWallet.Application.Validators;

namespace EWallet.Application.Tests.Validators;

public class TransferCommandValidatorTests
{
    private readonly TransferCommandValidator _validator = new();

    [Fact]
    public void Validate_AmountAboveThresholdWithoutOtp_Fails()
    {
        var cmd = new TransferCommand(
            SenderUserId: Guid.NewGuid(),
            ReceiverWalletId: Guid.NewGuid(),
            Amount: 600m,
            Currency: "USD",
            Description: null,
            OtpCode: null,
            IdempotencyKey: "idem-1");

        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var cmd = new TransferCommand(
            SenderUserId: Guid.NewGuid(),
            ReceiverWalletId: Guid.NewGuid(),
            Amount: 100m,
            Currency: "USD",
            Description: "test",
            OtpCode: null,
            IdempotencyKey: "idem-1");

        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }
}

