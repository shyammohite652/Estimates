using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Application.Estimates;
using ConstructionEstimator.Application.Security;
using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Users;

namespace ConstructionEstimator.UnitTests.Application;

/// <summary>
/// Hand-written in-memory ports. Small enough to read in one go, and they let the tests exercise the
/// real EstimateService + AccessGuard + domain without a database.
/// </summary>
internal sealed class World
{
    public Guid ProjectId { get; } = Guid.NewGuid();
    public Dictionary<Guid, Estimate> Estimates { get; } = [];
    public List<EstimateLine> Lines { get; } = [];
    public HashSet<(Guid User, Guid Project)> Assignments { get; } = [];
    public Dictionary<Guid, CatalogueItem> Catalogue { get; } = [];
    public int SaveCount { get; set; }
    public Queue<Exception> SaveFailures { get; } = new();
    public bool? LastIncludeLabour { get; set; }
    /// <summary>Simulates the committed database status differing from the tracked entity after a conflict.</summary>
    public EstimateStatus? StatusOverride { get; set; }

    public EstimateService ServiceFor(Guid userId, UserRole role)
    {
        var user = new FakeUser(userId, role);
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        return new EstimateService(user, new AccessGuard(user, new FakeAccess(this)), new FakeEstimates(this),
            new FakeQueries(this), new FakeCatalogue(this), new FakeUnitOfWork(this), clock);
    }

    public Estimate Add(Estimate e) { Estimates[e.Id] = e; return e; }
    public void Assign(Guid user) => Assignments.Add((user, ProjectId));
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed record FakeUser(Guid UserId, UserRole Role) : ICurrentUser;

internal sealed class FakeAccess(World w) : IProjectAccessRepository
{
    public Task<bool> IsAssignedAsync(Guid userId, Guid projectId, CancellationToken ct) =>
        Task.FromResult(w.Assignments.Contains((userId, projectId)));
}

internal sealed class FakeUnitOfWork(World w) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct)
    {
        if (w.SaveFailures.TryDequeue(out var ex)) throw ex;
        w.SaveCount++;
        return Task.CompletedTask;
    }

    public void DiscardChanges() { }
}

internal sealed class FakeCatalogue(World w) : ICatalogueRepository
{
    public Task<CatalogueItem?> GetWithRatesAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(w.Catalogue.GetValueOrDefault(id));
}

internal sealed class FakeEstimates(World w) : IEstimateRepository
{
    public Task<Estimate?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(w.Estimates.GetValueOrDefault(id));
    public Task<EstimateLine?> GetLineAsync(Guid estimateId, long lineId, CancellationToken ct) =>
        Task.FromResult(w.Lines.FirstOrDefault(l => l.EstimateId == estimateId && l.Id == lineId));
    public Task<EstimateLine?> GetLineForItemAsync(Guid estimateId, Guid itemId, CancellationToken ct) =>
        Task.FromResult(w.Lines.FirstOrDefault(l => l.EstimateId == estimateId && l.CatalogueItemId == itemId));
    public Task<int> CountLinesAsync(Guid estimateId, CancellationToken ct) => Task.FromResult(w.Lines.Count(l => l.EstimateId == estimateId));
    public Task<int> CountEstimatesInProjectAsync(Guid projectId, CancellationToken ct) =>
        Task.FromResult(w.Estimates.Values.Count(e => e.ProjectId == projectId));
    public void Add(Estimate estimate) => w.Add(estimate);
    public void AddLine(EstimateLine line) => w.Lines.Add(line);
    public void RemoveLine(EstimateLine line) => w.Lines.Remove(line);
}

internal sealed class FakeQueries(World w) : IEstimateQueries
{
    public Task<EstimateHeaderData?> GetHeaderAsync(Guid id, CancellationToken ct)
    {
        if (!w.Estimates.TryGetValue(id, out var e)) return Task.FromResult<EstimateHeaderData?>(null);
        var lines = w.Lines.Where(l => l.EstimateId == id).ToList();
        return Task.FromResult<EstimateHeaderData?>(new EstimateHeaderData(e.Id, e.ProjectId, "Project", e.Number,
            e.Description, e.PricingDate, e.Status, e.CreatedById, "Creator", e.CreatedAt, e.UpdatedAt, lines.Count,
            lines.Sum(l => l.NetAmount), lines.Sum(l => l.MarkupAmount), lines.Sum(l => l.LineTotal),
            lines.Sum(l => l.LabourAmount)));
    }

    public Task<EstimateStatus?> GetStatusAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(w.StatusOverride ?? (w.Estimates.TryGetValue(id, out var e) ? e.Status : (EstimateStatus?)null));

    public Task<PagedResult<EstimateLineDto>> GetLinesAsync(Guid id, PageRequest page, bool includeLabour, CancellationToken ct)
    {
        w.LastIncludeLabour = includeLabour;
        var items = w.Lines.Where(l => l.EstimateId == id).Select(l => ToDto(l, includeLabour)).ToList();
        return Task.FromResult(new PagedResult<EstimateLineDto>(items, page.Page, page.PageSize, items.Count));
    }

    public Task<EstimateLineDto?> GetLineAsync(Guid id, long lineId, bool includeLabour, CancellationToken ct)
    {
        w.LastIncludeLabour = includeLabour;
        var line = w.Lines.FirstOrDefault(l => l.EstimateId == id && l.Id == lineId);
        return Task.FromResult(line is null ? null : ToDto(line, includeLabour));
    }

    public Task<IReadOnlyList<EstimateHistoryDto>> GetHistoryAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EstimateHistoryDto>>([]);

    private static EstimateLineDto ToDto(EstimateLine l, bool labour) => new(l.Id, l.CatalogueItemId, l.ItemCode,
        l.ItemDescription, l.Unit, l.Quantity, l.MarkupPercent, l.UnitRate, l.RateEffectiveFrom,
        l.NetAmount, l.MarkupAmount, l.LineTotal,
        labour ? l.UnitLabourCost : null, labour ? l.LabourAmount : null);
}
