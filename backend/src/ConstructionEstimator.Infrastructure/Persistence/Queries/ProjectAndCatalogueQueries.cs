using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Catalogue;
using ConstructionEstimator.Application.Projects;
using Microsoft.EntityFrameworkCore;

namespace ConstructionEstimator.Infrastructure.Persistence.Queries;

internal sealed class ProjectQueries(AppDbContext db) : IProjectQueries
{
    internal sealed record EstimateTotalRow(Guid EstimateId, int LineCount, decimal Total);

    /// <summary>R18. Three set-based queries regardless of how many projects or estimates (no N+1).</summary>
    public async Task<IReadOnlyList<ProjectSummaryDto>> GetProjectsForUserAsync(Guid userId, CancellationToken ct)
    {
        var projects = await db.Projects.AsNoTracking()
            .Where(p => db.ProjectAssignments.Any(a => a.ProjectId == p.Id && a.UserId == userId))
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Code, p.Name, p.Client })
            .ToListAsync(ct);
        var projectIds = projects.Select(p => p.Id).ToList();

        var estimates = await db.Estimates.AsNoTracking()
            .Where(e => projectIds.Contains(e.ProjectId))
            .OrderBy(e => e.Number)
            .Select(e => new { e.Id, e.ProjectId, e.Number, e.Description, e.Status, e.PricingDate })
            .ToListAsync(ct);

        var estimateIds = estimates.Select(e => e.Id).ToList();
        var totals = await db.EstimateLines.AsNoTracking()
            .Where(l => estimateIds.Contains(l.EstimateId))
            .GroupBy(l => l.EstimateId)
            .Select(g => new EstimateTotalRow(g.Key, g.Count(), g.Sum(l => l.LineTotal)))
            .ToDictionaryAsync(t => t.EstimateId, ct);

        return projects.Select(p => new ProjectSummaryDto(p.Id, p.Code, p.Name, p.Client,
                estimates.Where(e => e.ProjectId == p.Id)
                    .Select(e =>
                    {
                        totals.TryGetValue(e.Id, out var t);
                        return new EstimateSummaryDto(e.Id, e.Number, e.Description, e.Status, e.PricingDate,
                            t?.LineCount ?? 0, t?.Total ?? 0m);
                    }).ToList()))
            .ToList();
    }
}

internal sealed class CatalogueQueries(AppDbContext db) : ICatalogueQueries
{
    public async Task<IReadOnlyList<CatalogueItemDto>> GetPricedOnAsync(DateOnly pricingDate, string? search, int take,
        bool includeLabour, CancellationToken ct)
    {
        var items = db.CatalogueItems.AsNoTracking();
        if (search is not null)
            items = items.Where(i => i.Code.StartsWith(search) || i.Description.Contains(search));

        var rows = await items
            .OrderBy(i => i.Code)
            .Take(take)
            .Select(i => new
            {
                i.Id, i.Code, i.Description, i.Unit,
                // R2: the rate in force on the pricing date.
                Rate = db.CatalogueRates
                    .Where(r => r.CatalogueItemId == i.Id && r.EffectiveFrom <= pricingDate)
                    .OrderByDescending(r => r.EffectiveFrom)
                    .Select(r => new { r.Rate, r.LabourComponent, r.EffectiveFrom })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r => new CatalogueItemDto(r.Id, r.Code, r.Description, r.Unit,
                r.Rate?.Rate, r.Rate?.EffectiveFrom,
                includeLabour ? r.Rate?.LabourComponent : null))
            .ToList();
    }
}
