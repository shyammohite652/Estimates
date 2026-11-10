using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Pricing;

namespace ConstructionEstimator.UnitTests.Domain;

internal static class TestData
{
    public static readonly Guid Estimator = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    public static readonly Guid Reviewer = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Manager = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    public static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 11, 10);

    /// <summary>The brief's R2 example: 1 450.00 from 1 Jan, 1 512.50 from 1 Jul.</summary>
    public static CatalogueItem Concrete()
    {
        var item = new CatalogueItem(Guid.NewGuid(), "CONC-C30", "Concrete Grade C30", "m3");
        item.AddRate(Guid.NewGuid(), new DateOnly(2026, 1, 1), Money.FromExact(1450.00m), Money.FromExact(320.00m));
        item.AddRate(Guid.NewGuid(), new DateOnly(2026, 7, 1), Money.FromExact(1512.50m), Money.FromExact(335.00m));
        return item;
    }

    public static Estimate Draft(DateOnly? pricingDate = null, Guid? createdBy = null) =>
        Estimate.Create(Guid.NewGuid(), Guid.NewGuid(), "EST-0001", "Test estimate",
            pricingDate ?? new DateOnly(2026, 3, 15), createdBy ?? Estimator, Now);

    public static Estimate WithOneLine(DateOnly? pricingDate = null, Guid? createdBy = null)
    {
        var e = Draft(pricingDate, createdBy);
        e.AddOrAdjustLine(Concrete(), null, Quantity.Create(10m), MarkupPercentage.Create(10m), Now);
        return e;
    }

    public static Estimate Submitted(Guid? createdBy = null)
    {
        var e = WithOneLine(createdBy: createdBy);
        e.Submit(1, Today, createdBy ?? Estimator, Now);
        return e;
    }

    public static Estimate Approved()
    {
        var e = Submitted();
        e.Approve(Reviewer, Now);
        return e;
    }
}
