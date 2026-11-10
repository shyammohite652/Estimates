using ConstructionEstimator.Domain.Common;
using ConstructionEstimator.Domain.Pricing;

namespace ConstructionEstimator.UnitTests.Domain;

/// <summary>R3, R4, R6 — the inputs and the money arithmetic.</summary>
public class PricingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.001)]
    public void Quantity_must_be_greater_than_zero(decimal value)
    {
        var ex = Assert.Throws<DomainValidationException>(() => Quantity.Create(value));
        Assert.Equal("quantity", ex.Field);
    }

    [Fact]
    public void Quantity_rejects_more_than_three_decimal_places_instead_of_rounding()
    {
        Assert.Throws<DomainValidationException>(() => Quantity.Create(1.2345m));
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(120.5)]
    [InlineData(1.2000)] // trailing zeros are not extra precision
    public void Quantity_accepts_up_to_three_decimal_places(decimal value)
    {
        Assert.Equal(value, Quantity.Create(value).Value);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(12.345)]
    public void Markup_must_be_0_to_100_with_two_decimal_places(decimal value)
    {
        var ex = Assert.Throws<DomainValidationException>(() => MarkupPercentage.Create(value));
        Assert.Equal("markupPercent", ex.Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(12.5)]
    public void Markup_accepts_bounds_and_two_decimals(decimal value)
    {
        Assert.Equal(value, MarkupPercentage.Create(value).Value);
    }

    [Fact]
    public void Line_amounts_follow_R4_with_commercial_rounding()
    {
        // net = 2.5 × 1450.00 = 3625.00; markup = 3625.00 × 12.5% = 453.125 → 453.13 (half away from zero).
        // Banker's rounding would give 453.12 — this test pins the decision in DESIGN.md A4.
        var amounts = LinePricing.Calculate(Quantity.Create(2.5m), Money.FromExact(1450.00m),
            Money.FromExact(320.00m), MarkupPercentage.Create(12.5m));

        Assert.Equal(3625.00m, amounts.Net.Amount);
        Assert.Equal(453.13m, amounts.Markup.Amount);
        Assert.Equal(4078.13m, amounts.Total.Amount);
        Assert.Equal(800.00m, amounts.Labour.Amount);
    }

    [Fact]
    public void Net_is_rounded_to_money_precision_before_markup_is_applied()
    {
        // 0.333 × 10.01 = 3.33333 → net 3.33; markup 50% of 3.33 = 1.665 → 1.67; total 5.00
        var amounts = LinePricing.Calculate(Quantity.Create(0.333m), Money.FromExact(10.01m),
            Money.Zero, MarkupPercentage.Create(50m));

        Assert.Equal(3.33m, amounts.Net.Amount);
        Assert.Equal(1.67m, amounts.Markup.Amount);
        Assert.Equal(5.00m, amounts.Total.Amount);
    }
}
