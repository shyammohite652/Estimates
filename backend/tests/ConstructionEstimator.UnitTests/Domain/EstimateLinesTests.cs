using ConstructionEstimator.Domain.Common;
using ConstructionEstimator.Domain.Pricing;
using static ConstructionEstimator.UnitTests.Domain.TestData;

namespace ConstructionEstimator.UnitTests.Domain;

/// <summary>R2, R4, R7, R10 — lines on an estimate.</summary>
public class EstimateLinesTests
{
    [Fact]
    public void New_line_is_priced_at_the_estimates_pricing_date_not_today()
    {
        // Pricing date is March; "today" in the test clock is November, when the July rate applies.
        var estimate = Draft(pricingDate: new DateOnly(2026, 3, 15));
        var line = estimate.AddOrAdjustLine(Concrete(), null, Quantity.Create(2m), MarkupPercentage.Create(0m), Now);

        Assert.Equal(1450.00m, line.UnitRate);
        Assert.Equal(new DateOnly(2026, 1, 1), line.RateEffectiveFrom);
        Assert.Equal(2900.00m, line.LineTotal);
    }

    [Fact]
    public void Adding_the_same_catalogue_item_again_adjusts_the_existing_line_rather_than_adding_one()
    {
        var item = Concrete();
        var estimate = Draft();
        var first = estimate.AddOrAdjustLine(item, null, Quantity.Create(10m), MarkupPercentage.Create(5m), Now);

        var second = estimate.AddOrAdjustLine(item, first, Quantity.Create(4m), MarkupPercentage.Create(10m), Now);

        Assert.Same(first, second);
        Assert.Equal(4m, second.Quantity);           // replaced, not 14 (DESIGN.md A5)
        Assert.Equal(10m, second.MarkupPercent);
        Assert.Equal(6380.00m, second.LineTotal);    // 4 × 1450 = 5800 + 10% = 6380
    }

    public static TheoryData<string> LockedStatuses => new() { "Submitted", "Approved", "Rejected" };

    [Theory]
    [MemberData(nameof(LockedStatuses))]
    public void Lines_cannot_be_added_changed_or_removed_outside_Draft(string status)
    {
        var estimate = Draft();
        var line = estimate.AddOrAdjustLine(Concrete(), null, Quantity.Create(1m), MarkupPercentage.Create(0m), Now);
        estimate.Submit(1, Today, Estimator, Now);
        if (status == "Approved") estimate.Approve(Reviewer, Now);
        if (status == "Rejected") estimate.Reject(Reviewer, "No", Now);

        Assert.Throws<EstimateNotEditableException>(() =>
            estimate.AddOrAdjustLine(Concrete(), null, Quantity.Create(1m), MarkupPercentage.Create(0m), Now));
        Assert.Throws<EstimateNotEditableException>(() =>
            estimate.ChangeLine(line, Quantity.Create(2m), MarkupPercentage.Create(0m), Now));
        Assert.Throws<EstimateNotEditableException>(() => estimate.RemoveLine(line, Now));
        Assert.Equal(1m, line.Quantity);
    }
}
