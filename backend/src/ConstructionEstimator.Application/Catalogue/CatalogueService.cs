using System.Text.Json.Serialization;
using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Security;

namespace ConstructionEstimator.Application.Catalogue;

/// <summary>A catalogue item priced on a given date (R2). Rate is null when no rate is effective yet.</summary>
public sealed record CatalogueItemDto(Guid Id, string Code, string Description, string Unit,
    decimal? Rate, DateOnly? RateEffectiveFrom,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? LabourComponent);

public sealed class CatalogueService(ICurrentUser currentUser, ICatalogueQueries queries)
{
    public const int MaxResults = 50;

    /// <summary>Searchable rather than a full list: with a large catalogue the picker must not download everything.</summary>
    public Task<IReadOnlyList<CatalogueItemDto>> SearchPricedOnAsync(DateOnly pricingDate, string? search, CancellationToken ct) =>
        queries.GetPricedOnAsync(pricingDate, string.IsNullOrWhiteSpace(search) ? null : search.Trim(), MaxResults,
            Permissions.CanViewLabourCost(currentUser.Role), ct);
}
