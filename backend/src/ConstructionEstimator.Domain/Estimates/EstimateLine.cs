using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Pricing;

namespace ConstructionEstimator.Domain.Estimates;

/// <summary>
/// One priced line. Snapshots the catalogue item and the rate effective on the estimate's
/// pricing date (R2) so later catalogue changes never alter an existing line.
/// Created and changed only through <see cref="Estimate"/>, which enforces the Draft-only rule (R10).
///
/// Stored numbers are plain decimals so the database can aggregate them (R5, R19), but they can only be
/// written from validated value objects (<see cref="Quantity"/>, <see cref="MarkupPercentage"/>,
/// <see cref="Money"/>) — there is no path that sets an unvalidated or unrounded amount.
/// </summary>
public sealed class EstimateLine
{
    public long Id { get; private set; }
    public Guid EstimateId { get; private set; }
    public Guid CatalogueItemId { get; private set; }

    // Snapshot of the catalogue at pricing time.
    public string ItemCode { get; private set; } = null!;
    public string ItemDescription { get; private set; } = null!;
    public string Unit { get; private set; } = null!;
    public DateOnly RateEffectiveFrom { get; private set; }
    public decimal UnitRate { get; private set; }
    public decimal UnitLabourCost { get; private set; }

    // Inputs.
    public decimal Quantity { get; private set; }
    public decimal MarkupPercent { get; private set; }

    // Derived (R4), stored so totals can be summed in the database (R5, R19).
    public decimal NetAmount { get; private set; }
    public decimal MarkupAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal LabourAmount { get; private set; }

    private EstimateLine() { }

    internal EstimateLine(Guid estimateId, CatalogueItem item, CatalogueRate rate, Quantity quantity, MarkupPercentage markup)
    {
        EstimateId = estimateId;
        CatalogueItemId = item.Id;
        ItemCode = item.Code;
        ItemDescription = item.Description;
        Unit = item.Unit;
        RateEffectiveFrom = rate.EffectiveFrom;
        UnitRate = rate.Rate;
        UnitLabourCost = rate.LabourComponent;
        Reprice(quantity, markup);
    }

    internal void Reprice(Quantity quantity, MarkupPercentage markup)
    {
        Quantity = quantity.Value;
        MarkupPercent = markup.Value;
        var amounts = LinePricing.Calculate(quantity, Money.FromExact(UnitRate), Money.FromExact(UnitLabourCost), markup);
        NetAmount = amounts.Net.Amount;
        MarkupAmount = amounts.Markup.Amount;
        LineTotal = amounts.Total.Amount;
        LabourAmount = amounts.Labour.Amount;
    }
}
