using FluentAssertions;
using EWallet.Application.Commands;
using EWallet.Application.Validators;

namespace EWallet.Application.Tests.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        var cmd = new RegisterCommand(
            Email: "user@example.com",
            PhoneNumber: "+15551234567",
            FullName: "Jane Doe",
            Password: "Strong@Pass1!",
            ConfirmPassword: "Strong@Pass1!");

        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MismatchedPasswords_HasErrors()
    {
        var cmd = new RegisterCommand(
            Email: "user@example.com",
            PhoneNumber: "+15551234567",
            FullName: "Jane Doe",
            Password: "Strong@Pass1!",
            ConfirmPassword: "Different@Pass2!");

        var result = _validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}

