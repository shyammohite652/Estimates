using ConstructionEstimator.Domain.Common;
using ConstructionEstimator.Domain.Pricing;

namespace ConstructionEstimator.UnitTests.Domain;

/// <summary>R1, R2 — effective-dated rates.</summary>
public class CatalogueTests
{
    [Theory]
    [InlineData("2026-01-01", 1450.00)]
    [InlineData("2026-06-30", 1450.00)]
    [InlineData("2026-07-01", 1512.50)]
    [InlineData("2027-03-01", 1512.50)]
    public void Rate_is_the_one_in_force_on_the_pricing_date(string pricingDate, decimal expected)
    {
        var rate = TestData.Concrete().RateEffectiveOn(DateOnly.Parse(pricingDate));
        Assert.Equal(expected, rate.Rate);
    }

    [Fact]
    public void No_rate_before_the_first_effective_date_is_a_business_rule_violation()
    {
        var ex = Assert.Throws<BusinessRuleViolationException>(() => TestData.Concrete().RateEffectiveOn(new DateOnly(2025, 12, 31)));
        Assert.Equal("no_effective_rate", ex.Code);
    }

    [Fact]
    public void Two_rates_cannot_share_an_effective_date()
    {
        var item = TestData.Concrete();
        Assert.Throws<BusinessRuleViolationException>(() =>
            item.AddRate(Guid.NewGuid(), new DateOnly(2026, 7, 1), Money.FromExact(1.00m), Money.Zero));
    }

    [Fact]
    public void Labour_component_cannot_exceed_the_rate()
    {
        var item = TestData.Concrete();
        Assert.Throws<DomainValidationException>(() =>
            item.AddRate(Guid.NewGuid(), new DateOnly(2027, 1, 1), Money.FromExact(10.00m), Money.FromExact(10.01m)));
    }
}
