using EWallet.Domain.Exceptions;

namespace EWallet.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount with an ISO-4217 currency code.
/// All amounts are rounded to 2 decimal places using banker's-away rounding.
/// </summary>
public record Money
{
    /// <summary>The monetary amount, always rounded to 2 decimal places and non-negative.</summary>
    public decimal Amount { get; init; }

    /// <summary>The ISO-4217 currency code (3 uppercase letters).</summary>
    public string Currency { get; init; }

    /// <summary>
    /// Creates a new <see cref="Money"/> instance.
    /// </summary>
    /// <param name="amount">The monetary amount. Must be &gt;= 0 and &lt;= 10,000,000.</param>
    /// <param name="currency">A 3-character ISO-4217 currency code. Defaults to "USD".</param>
    /// <exception cref="DomainException">Thrown when <paramref name="amount"/> is negative, exceeds the maximum, or <paramref name="currency"/> is invalid.</exception>
    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative");
        if (amount > 10_000_000)
            throw new DomainException("Amount exceeds maximum limit");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Invalid currency code");

        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>Returns a zero-valued <see cref="Money"/> for the specified currency.</summary>
    /// <param name="currency">ISO-4217 currency code. Defaults to "USD".</param>
    public static Money Zero(string currency = "USD") => new(0, currency);

    /// <summary>Returns <c>true</c> when the amount is exactly zero.</summary>
    public bool IsZero => Amount == 0;

    /// <summary>
    /// Adds another <see cref="Money"/> to this instance.
    /// Both operands must share the same currency.
    /// </summary>
    /// <exception cref="DomainException">Thrown on currency mismatch.</exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Subtracts <paramref name="other"/> from this instance.
    /// Both operands must share the same currency.
    /// The result must be non-negative; the <see cref="Money"/> constructor enforces this.
    /// </summary>
    /// <exception cref="DomainException">Thrown on currency mismatch or if the result is negative.</exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Returns <c>true</c> when this amount is strictly greater than <paramref name="other"/>.</summary>
    /// <exception cref="DomainException">Thrown on currency mismatch.</exception>
    public bool IsGreaterThan(Money other)
    {
        EnsureSameCurrency(other);
        return Amount > other.Amount;
    }

    /// <summary>Returns <c>true</c> when this amount is greater than or equal to <paramref name="other"/>.</summary>
    /// <exception cref="DomainException">Thrown on currency mismatch.</exception>
    public bool IsGreaterThanOrEqual(Money other)
    {
        EnsureSameCurrency(other);
        return Amount >= other.Amount;
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Currency mismatch: {Currency} vs {other.Currency}");
    }

    /// <summary>Returns a human-readable representation such as "10.50 USD".</summary>
    public override string ToString() => $"{Amount:F2} {Currency}";
}
