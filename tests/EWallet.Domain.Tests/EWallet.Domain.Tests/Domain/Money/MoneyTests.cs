namespace EWallet.Domain.Tests.Domain.Money;

public sealed class MoneyTests
{
    // -----------------------------------------------------------------------
    // Constructor — negative amount
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_NegativeAmount_ThrowsDomainException()
    {
        // Act
        var act = () => new Money(-1m, "USD");

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*negative*");
    }

    // -----------------------------------------------------------------------
    // Constructor — rounding (MidpointRounding.AwayFromZero to 2 dp)
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_RoundsToTwoDecimalPlacesAwayFromZero()
    {
        // Act
        var money = new Money(10.505m, "USD");

        // Assert
        money.Amount.Should().Be(10.51m);
    }

    [Fact]
    public void Constructor_ValidAmount_SetsAmount()
    {
        var money = new Money(10m, "USD");
        money.Amount.Should().Be(10m);
    }

    // -----------------------------------------------------------------------
    // IsZero
    // -----------------------------------------------------------------------

    [Fact]
    public void IsZero_WhenAmountIsZero_ReturnsTrue()
    {
        var money = new Money(0m, "USD");
        money.IsZero.Should().BeTrue();
    }

    [Fact]
    public void IsZero_WhenAmountIsPositive_ReturnsFalse()
    {
        var money = new Money(1m, "USD");
        money.IsZero.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Add
    // -----------------------------------------------------------------------

    [Fact]
    public void Add_SameCurrency_ReturnsSummedAmount()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m, "USD");

        var result = a.Add(b);

        result.Amount.Should().Be(15m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsDomainExceptionWithCurrencyMismatch()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m, "EUR");

        var act = () => a.Add(b);

        act.Should().Throw<DomainException>()
            .WithMessage("*Currency mismatch*");
    }

    // -----------------------------------------------------------------------
    // Subtract
    // -----------------------------------------------------------------------

    [Fact]
    public void Subtract_SameCurrency_ReturnsCorrectAmount()
    {
        var a = new Money(10m, "USD");
        var b = new Money(4m, "USD");

        var result = a.Subtract(b);

        result.Amount.Should().Be(6m);
    }

    [Fact]
    public void Subtract_ResultBelowZero_ThrowsDomainException()
    {
        var a = new Money(5m, "USD");
        var b = new Money(10m, "USD");

        var act = () => a.Subtract(b);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Subtract_DifferentCurrency_ThrowsDomainException()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m, "EUR");

        var act = () => a.Subtract(b);

        act.Should().Throw<DomainException>();
    }

    // -----------------------------------------------------------------------
    // Currency code validation [Theory]
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("USD", true)]
    [InlineData("EUR", true)]
    [InlineData("GBP", true)]
    [InlineData("US", false)]   // too short
    [InlineData("USDD", false)] // too long
    [InlineData("", false)]     // empty
    public void Constructor_CurrencyCodeValidation(string currency, bool isValid)
    {
        var act = () => new Money(10m, currency);

        if (isValid)
            act.Should().NotThrow();
        else
            act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_NullCurrency_ThrowsDomainException()
    {
        var act = () => new Money(10m, null!);
        act.Should().Throw<DomainException>();
    }

    // -----------------------------------------------------------------------
    // Equality
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_SameAmountAndCurrency_AreEqual()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentAmount_AreNotEqual()
    {
        var a = new Money(10m, "USD");
        var b = new Money(20m, "USD");
        a.Should().NotBe(b);
    }
}
