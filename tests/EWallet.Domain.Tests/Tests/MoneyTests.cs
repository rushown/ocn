using FluentAssertions;
using EWallet.Domain.Exceptions;
using EWallet.Domain.ValueObjects;

namespace EWallet.Domain.Tests.Tests;

public class MoneyTests
{
    // ─── Constructor ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NegativeAmount_ThrowsDomainException()
    {
        // Act
        var act = () => new Money(-1m, "USD");

        // Assert
        act.Should().Throw<DomainException>(
            because: "money cannot represent a negative value");
    }

    [Fact]
    public void Constructor_ValidAmount_RoundsToTwoDecimalPlacesAwayFromZero()
    {
        // Arrange – 10.505 rounds to 10.51 under MidpointRounding.AwayFromZero
        var money = new Money(10.505m, "USD");

        // Assert
        money.Amount.Should().Be(10.51m);
    }

    [Fact]
    public void Constructor_ZeroAmount_IsAllowed()
    {
        var act = () => new Money(0m, "USD");

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ValidPositiveAmount_SetsAmountCorrectly()
    {
        var money = new Money(42.50m, "USD");

        money.Amount.Should().Be(42.50m);
        money.Currency.Should().Be("USD");
    }

    // ─── Currency code validation ─────────────────────────────────────────────

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("NPR")]
    public void Constructor_ValidThreeLetterCurrencyCode_DoesNotThrow(string currency)
    {
        var act = () => new Money(1m, currency);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("US")]     // too short
    [InlineData("USDD")]   // too long
    [InlineData("")]       // empty
    [InlineData(null)]     // null
    [InlineData("us1")]    // non-alpha
    public void Constructor_InvalidCurrencyCode_ThrowsDomainException(string? currency)
    {
        var act = () => new Money(1m, currency!);

        act.Should().Throw<DomainException>(
            because: $"'{currency}' is not a valid ISO 4217 currency code");
    }

    // ─── Add ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_SameCurrency_ReturnsSummedAmount()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m,  "USD");

        var result = a.Add(b);

        result.Amount.Should().Be(15m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsDomainExceptionWithCurrencyMismatchMessage()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m,  "EUR");

        var act = () => a.Add(b);

        act.Should().Throw<DomainException>()
           .WithMessage("*Currency mismatch*",
               because: "adding amounts in different currencies is undefined without an exchange rate");
    }

    // ─── Subtract ────────────────────────────────────────────────────────────

    [Fact]
    public void Subtract_SameCurrency_SufficientAmount_ReturnsRemainder()
    {
        var a = new Money(20m, "USD");
        var b = new Money(8m,  "USD");

        var result = a.Subtract(b);

        result.Amount.Should().Be(12m);
    }

    [Fact]
    public void Subtract_ResultBelowZero_ThrowsDomainException()
    {
        var a = new Money(5m,  "USD");
        var b = new Money(10m, "USD");

        var act = () => a.Subtract(b);

        act.Should().Throw<DomainException>(
            because: "subtraction cannot produce a negative Money value");
    }

    [Fact]
    public void Subtract_DifferentCurrency_ThrowsDomainException()
    {
        var a = new Money(20m, "USD");
        var b = new Money(5m,  "EUR");

        var act = () => a.Subtract(b);

        act.Should().Throw<DomainException>()
           .WithMessage("*Currency mismatch*");
    }

    // ─── IsZero ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsZero_ZeroAmount_ReturnsTrue()
    {
        var money = new Money(0m, "USD");

        money.IsZero.Should().BeTrue();
    }

    [Fact]
    public void IsZero_NonZeroAmount_ReturnsFalse()
    {
        var money = new Money(0.01m, "USD");

        money.IsZero.Should().BeFalse();
    }

    // ─── Equality ────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_SameAmountAndCurrency_AreEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(100m, "USD");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentAmount_AreNotEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(200m, "USD");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_SameAmountDifferentCurrency_AreNotEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(100m, "EUR");

        a.Should().NotBe(b);
    }
}
