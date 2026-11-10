using ConstructionEstimator.Domain.Common;

namespace ConstructionEstimator.Domain.Pricing;

/// <summary>
/// A monetary amount, always held to exactly 2 decimal places (R4).
/// Rounding is commercial (half away from zero), never banker's rounding.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int DecimalPlaces = 2;
    /// <summary>Largest unit amount accepted (keeps quantity × rate inside decimal(18,2)).</summary>
    public const decimal MaxUnitAmount = 9_999_999.99m;

    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Money Zero { get; } = new(0m);

    /// <summary>Rounds a computed value to money precision.</summary>
    public static Money Round(decimal value) =>
        new(decimal.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero));

    /// <summary>Creates money from an exact amount that must already be at money precision (e.g. a catalogue rate).</summary>
    public static Money FromExact(decimal amount, string field = "amount")
    {
        if (amount < 0)
            throw new DomainValidationException(field, $"{field} cannot be negative.");
        if (!Guard.HasAtMostDecimalPlaces(amount, DecimalPlaces))
            throw new DomainValidationException(field, $"{field} must have at most {DecimalPlaces} decimal places.");
        return new Money(decimal.Round(amount, DecimalPlaces));
    }

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;

    public override string ToString() => Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
