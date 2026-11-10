using ConstructionEstimator.Domain.Common;

namespace ConstructionEstimator.Domain.Pricing;

/// <summary>A line quantity: strictly positive (R6) with at most 3 decimal places (R3).</summary>
public readonly record struct Quantity
{
    public const int DecimalPlaces = 3;
    public const decimal MaxValue = 99_999_999.999m;

    public decimal Value { get; }

    private Quantity(decimal value) => Value = value;

    public static Quantity Create(decimal value)
    {
        if (value <= 0)
            throw new DomainValidationException("quantity", "Quantity must be greater than zero.");
        if (!Guard.HasAtMostDecimalPlaces(value, DecimalPlaces))
            throw new DomainValidationException("quantity", $"Quantity must have at most {DecimalPlaces} decimal places.");
        if (value > MaxValue)
            throw new DomainValidationException("quantity", $"Quantity must not exceed {MaxValue}.");
        return new Quantity(decimal.Round(value, DecimalPlaces));
    }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
