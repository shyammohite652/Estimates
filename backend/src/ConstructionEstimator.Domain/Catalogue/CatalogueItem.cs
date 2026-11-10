using ConstructionEstimator.Domain.Common;
using ConstructionEstimator.Domain.Pricing;

namespace ConstructionEstimator.Domain.Catalogue;

/// <summary>R1: a priced item in the rate catalogue. Aggregate root for its effective-dated rates (R2).</summary>
public sealed class CatalogueItem
{
    private readonly List<CatalogueRate> _rates = [];

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Unit { get; private set; } = null!;
    public IReadOnlyCollection<CatalogueRate> Rates => _rates;

    private CatalogueItem() { }

    public CatalogueItem(Guid id, string code, string description, string unit)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Code = Guard.RequiredText(code, nameof(code), 32).ToUpperInvariant();
        Description = Guard.RequiredText(description, nameof(description), 200);
        Unit = Guard.RequiredText(unit, nameof(unit), 16);
    }

    /// <summary>Adds a rate effective from <paramref name="effectiveFrom"/> until the next rate.</summary>
    public CatalogueRate AddRate(Guid rateId, DateOnly effectiveFrom, Money rate, Money labourComponent)
    {
        if (_rates.Any(r => r.EffectiveFrom == effectiveFrom))
            throw new BusinessRuleViolationException("duplicate_rate_date",
                $"{Code} already has a rate effective from {effectiveFrom:yyyy-MM-dd}.");
        var created = new CatalogueRate(rateId, Id, effectiveFrom, rate, labourComponent);
        _rates.Add(created);
        return created;
    }

    /// <summary>
    /// R2: the rate in force on <paramref name="pricingDate"/> — the one with the latest
    /// effective-from date on or before it. Never "today's" rate.
    /// </summary>
    public CatalogueRate RateEffectiveOn(DateOnly pricingDate) =>
        _rates.Where(r => r.EffectiveFrom <= pricingDate)
              .MaxBy(r => r.EffectiveFrom)
        ?? throw new BusinessRuleViolationException("no_effective_rate",
            $"{Code} has no rate effective on {pricingDate:yyyy-MM-dd}.");
}

/// <summary>R1/R2: a rate and its labour component, effective from a date.</summary>
public sealed class CatalogueRate
{
    public Guid Id { get; private set; }
    public Guid CatalogueItemId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal Rate { get; private set; }
    /// <summary>The labour part of <see cref="Rate"/>. Hidden from Estimators (R17).</summary>
    public decimal LabourComponent { get; private set; }

    private CatalogueRate() { }

    internal CatalogueRate(Guid id, Guid catalogueItemId, DateOnly effectiveFrom, Money rate, Money labourComponent)
    {
        if (rate.Amount > Money.MaxUnitAmount)
            throw new DomainValidationException(nameof(rate), $"Rate must not exceed {Money.MaxUnitAmount}.");
        if (labourComponent > rate)
            throw new DomainValidationException(nameof(labourComponent), "Labour component cannot exceed the rate.");
        Id = Guard.NotEmpty(id, nameof(id));
        CatalogueItemId = catalogueItemId;
        EffectiveFrom = effectiveFrom;
        Rate = rate.Amount;
        LabourComponent = labourComponent.Amount;
    }
}
