using FluentValidation.TestHelper;
using EWallet.Application.Commands.Auth;
using EWallet.Application.Validators;

namespace EWallet.Application.Tests.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    // ─── Email ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    public void Email_Invalid_ShouldFail(string? email)
    {
        var command = ValidCommand() with { Email = email! };

        _validator.TestValidate(command)
                  .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_Valid_ShouldPass()
    {
        var command = ValidCommand();

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ─── Password strength ───────────────────────────────────────────────────

    [Theory]
    [InlineData("short1!")]          // < 8 chars
    [InlineData("nouppercase1!")]    // no uppercase
    [InlineData("NOLOWERCASE1!")]    // no lowercase
    [InlineData("NoSpecialChar1")]   // no special char
    [InlineData("NoNumber!Abc")]     // no digit
    public void Password_Weak_ShouldFail(string password)
    {
        var command = ValidCommand() with { Password = password, ConfirmPassword = password };

        _validator.TestValidate(command)
                  .ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_Strong_ShouldPass()
    {
        var command = ValidCommand();

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // ─── Password confirmation ───────────────────────────────────────────────

    [Fact]
    public void ConfirmPassword_NotMatchingPassword_ShouldFail()
    {
        var command = ValidCommand() with { ConfirmPassword = "DifferentP@ss1" };

        _validator.TestValidate(command)
                  .ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    [Fact]
    public void ConfirmPassword_Matching_ShouldPass()
    {
        var command = ValidCommand();

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    // ─── Phone number ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("123")]            // too short
    [InlineData("abcdefghij")]    // non-numeric
    [InlineData("+1 (555) abc")]  // mixed invalid
    public void PhoneNumber_InvalidFormat_ShouldFail(string phone)
    {
        var command = ValidCommand() with { PhoneNumber = phone };

        _validator.TestValidate(command)
                  .ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Theory]
    [InlineData("+14155552671")]
    [InlineData("+977984123456")]
    public void PhoneNumber_ValidE164Format_ShouldPass(string phone)
    {
        var command = ValidCommand() with { PhoneNumber = phone };

        _validator.TestValidate(command)
                  .ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static RegisterCommand ValidCommand() => new()
    {
        Email           = "user@ewallet.test",
        Password        = "Str0ng@Pass!",
        ConfirmPassword = "Str0ng@Pass!",
        PhoneNumber     = "+14155552671",
        FullName        = "Jane Doe",
    };
}
