using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Application.Estimates;
using ConstructionEstimator.Domain.Estimates;
using Microsoft.EntityFrameworkCore;

namespace ConstructionEstimator.Infrastructure.Persistence.Queries;

/// <summary>
/// Read side. No tracking, projections only, and totals computed by SQL Server (R5) over the
/// covering index — the 50 000 lines (R19) never cross the wire to compute a header.
/// </summary>
internal sealed class EstimateQueries(AppDbContext db) : IEstimateQueries
{
    internal sealed record LineTotalsRow(int LineCount, decimal NetTotal, decimal MarkupTotal, decimal Total, decimal LabourTotal);

    public async Task<EstimateHeaderData?> GetHeaderAsync(Guid estimateId, CancellationToken ct)
    {
        var header = await db.Estimates.AsNoTracking()
            .Where(e => e.Id == estimateId)
            .Select(e => new
            {
                e.Id, e.ProjectId,
                ProjectName = db.Projects.Where(p => p.Id == e.ProjectId).Select(p => p.Name).First(),
                e.Number, e.Description, e.PricingDate, e.Status, e.CreatedById,
                CreatedByName = db.Users.Where(u => u.Id == e.CreatedById).Select(u => u.DisplayName).First(),
                e.CreatedAt, e.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);
        if (header is null) return null;

        var totals = await GetTotalsAsync(estimateId, ct);
        return new EstimateHeaderData(header.Id, header.ProjectId, header.ProjectName, header.Number,
            header.Description, header.PricingDate, header.Status, header.CreatedById, header.CreatedByName,
            header.CreatedAt, header.UpdatedAt, totals.LineCount, totals.NetTotal, totals.MarkupTotal,
            totals.Total, totals.LabourTotal);
    }

    /// <summary>One aggregate query, answered from the covering index IX_EstimateLines_EstimateId_Totals.</summary>
    private async Task<LineTotalsRow> GetTotalsAsync(Guid estimateId, CancellationToken ct)
    {
        var totals = await db.EstimateLines.AsNoTracking()
            .Where(l => l.EstimateId == estimateId)
            .GroupBy(l => l.EstimateId)
            .Select(g => new LineTotalsRow(
                g.Count(),
                g.Sum(l => l.NetAmount),
                g.Sum(l => l.MarkupAmount),
                g.Sum(l => l.LineTotal),
                g.Sum(l => l.LabourAmount)))
            .FirstOrDefaultAsync(ct);
        return totals ?? new LineTotalsRow(0, 0m, 0m, 0m, 0m);
    }

    public Task<EstimateStatus?> GetStatusAsync(Guid estimateId, CancellationToken ct) =>
        db.Estimates.AsNoTracking().Where(e => e.Id == estimateId).Select(e => (EstimateStatus?)e.Status).FirstOrDefaultAsync(ct);

    public async Task<PagedResult<EstimateLineDto>> GetLinesAsync(Guid estimateId, PageRequest page, bool includeLabour, CancellationToken ct)
    {
        var lines = db.EstimateLines.AsNoTracking().Where(l => l.EstimateId == estimateId);
        var total = await lines.CountAsync(ct);
        var paged = lines.OrderBy(l => l.Id).Skip(page.Skip).Take(page.PageSize);
        var items = await Project(paged, includeLabour).ToListAsync(ct);
        return new PagedResult<EstimateLineDto>(items, page.Page, page.PageSize, total);
    }

    public Task<EstimateLineDto?> GetLineAsync(Guid estimateId, long lineId, bool includeLabour, CancellationToken ct) =>
        Project(db.EstimateLines.AsNoTracking().Where(l => l.EstimateId == estimateId && l.Id == lineId), includeLabour)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Two separate projections so that, for roles without R17 permission, the labour columns are
    /// not even part of the SQL SELECT.
    /// </summary>
    private static IQueryable<EstimateLineDto> Project(IQueryable<EstimateLine> q, bool includeLabour) =>
        includeLabour
            ? q.Select(l => new EstimateLineDto(l.Id, l.CatalogueItemId, l.ItemCode, l.ItemDescription, l.Unit,
                l.Quantity, l.MarkupPercent, l.UnitRate, l.RateEffectiveFrom,
                l.NetAmount, l.MarkupAmount, l.LineTotal,
                l.UnitLabourCost, l.LabourAmount))
            : q.Select(l => new EstimateLineDto(l.Id, l.CatalogueItemId, l.ItemCode, l.ItemDescription, l.Unit,
                l.Quantity, l.MarkupPercent, l.UnitRate, l.RateEffectiveFrom,
                l.NetAmount, l.MarkupAmount, l.LineTotal,
                null, null));

    public async Task<IReadOnlyList<EstimateHistoryDto>> GetHistoryAsync(Guid estimateId, CancellationToken ct) =>
        await db.EstimateHistory.AsNoTracking()
            .Where(h => h.EstimateId == estimateId)
            .OrderBy(h => h.OccurredAt).ThenBy(h => h.Id)
            .Join(db.Users, h => h.UserId, u => u.Id, (h, u) => new EstimateHistoryDto(
                h.Id, h.Action, h.FromStatus, h.ToStatus, h.UserId, u.DisplayName, h.OccurredAt, h.Comment))
            .ToListAsync(ct);
}
