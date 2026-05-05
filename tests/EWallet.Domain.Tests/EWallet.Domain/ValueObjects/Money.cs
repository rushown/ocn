using EWallet.Domain.Exceptions;

namespace EWallet.Domain.ValueObjects;

/// <summary>
/// Immutable money value object. Rounds to 2 decimal places (AwayFromZero).
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        ValidateCurrency(currency);

        if (amount < 0)
            throw new DomainException("Money amount cannot be negative.");

        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public bool IsZero => Amount == 0m;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        var result = Amount - other.Amount;
        if (result < 0)
            throw new DomainException("Subtraction would result in a negative amount.");

        return new Money(result, Currency);
    }

    public bool IsGreaterThanOrEqualTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount >= other.Amount;
    }

    // -----------------------------------------------------------------------

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Currency mismatch: {Currency} vs {other.Currency}.");
    }

    private static void ValidateCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException($"Invalid currency code: '{currency}'. Must be exactly 3 characters.");
    }
}
