namespace ConstructionEstimator.Domain.Pricing;

/// <summary>The derived monetary amounts of one estimate line.</summary>
public readonly record struct LineAmounts(Money Net, Money Markup, Money Total, Money Labour);

/// <summary>
/// R4 arithmetic, in one place:
///   net    = round(quantity × rate, 2)
///   markup = round(net × markup% / 100, 2)
///   total  = net + markup
///   labour = round(quantity × labour component, 2)   (visible only to permitted roles, R17)
/// Each step is rounded so that stored, displayed and summed figures always agree.
/// </summary>
public static class LinePricing
{
    public static LineAmounts Calculate(Quantity quantity, Money unitRate, Money unitLabourCost, MarkupPercentage markup)
    {
        var net = Money.Round(quantity.Value * unitRate.Amount);
        var markupAmount = Money.Round(net.Amount * markup.Value / 100m);
        var labour = Money.Round(quantity.Value * unitLabourCost.Amount);
        return new LineAmounts(net, markupAmount, net + markupAmount, labour);
    }
}
