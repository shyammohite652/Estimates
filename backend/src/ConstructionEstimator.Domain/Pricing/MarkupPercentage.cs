using ConstructionEstimator.Domain.Common;

namespace ConstructionEstimator.Domain.Pricing;

/// <summary>A markup percentage between 0 and 100 inclusive, with at most 2 decimal places (R3).</summary>
public readonly record struct MarkupPercentage
{
    public const int DecimalPlaces = 2;

    public decimal Value { get; }

    private MarkupPercentage(decimal value) => Value = value;

    public static MarkupPercentage Create(decimal value)
    {
        if (value is < 0 or > 100)
            throw new DomainValidationException("markupPercent", "Markup must be between 0 and 100.");
        if (!Guard.HasAtMostDecimalPlaces(value, DecimalPlaces))
            throw new DomainValidationException("markupPercent", $"Markup must have at most {DecimalPlaces} decimal places.");
        return new MarkupPercentage(decimal.Round(value, DecimalPlaces));
    }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
