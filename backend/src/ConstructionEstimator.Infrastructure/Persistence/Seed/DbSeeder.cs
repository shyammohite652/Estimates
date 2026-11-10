using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Pricing;
using ConstructionEstimator.Domain.Projects;
using ConstructionEstimator.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static ConstructionEstimator.Infrastructure.Persistence.Seed.SeedIds;

namespace ConstructionEstimator.Infrastructure.Persistence.Seed;

/// <summary>
/// Development seed data, written through the domain model so seeded estimates obey the same rules
/// (and carry the same audit trail) as ones created through the API.
/// </summary>
public sealed class DbSeeder(AppDbContext db, ILogger<DbSeeder> logger)
{
    public async Task MigrateAndSeedAsync(int largeEstimateLines, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct); // in-memory demo mode only

        if (await db.Users.AnyAsync(ct))
        {
            logger.LogInformation("Seed data already present; skipping");
            return;
        }

        // All-or-nothing: a half-seeded database is worse than an empty one.
        // The SQL Server provider is configured with a retrying execution strategy (EnableRetryOnFailure),
        // which forbids a bare BeginTransaction: the whole transaction must run inside the strategy so that
        // a transient failure replays it from the start. Hence the tracker is cleared on every attempt.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            SeedReferenceData(out var items);
            await db.SaveChangesAsync(ct);
            SeedEstimates(items);
            await db.SaveChangesAsync(ct);
            if (largeEstimateLines > 0)
                await SeedLargeEstimateAsync(largeEstimateLines, ct);
            if (tx is not null) await tx.CommitAsync(ct);
        });
        logger.LogInformation("Seeded development data (large estimate lines: {Lines})", largeEstimateLines);
    }

    private void SeedReferenceData(out Dictionary<Guid, CatalogueItem> items)
    {
        db.Users.AddRange(
            new User(Estimator1, "estimator1@example.com", "Asha Estimator", UserRole.Estimator),
            new User(Estimator2, "estimator2@example.com", "Ben Estimator", UserRole.Estimator),
            new User(Reviewer1, "reviewer1@example.com", "Chitra Reviewer", UserRole.Reviewer),
            new User(Reviewer2, "reviewer2@example.com", "Dev Reviewer", UserRole.Reviewer),
            new User(Viewer1, "viewer1@example.com", "Esha Viewer", UserRole.Viewer),
            new User(Manager1, "manager1@example.com", "Farhan Manager", UserRole.ProjectManager));

        var residential = new Project(Residential, "RES-001", "Construction of Residential Building", "Greenfield Homes Ltd");
        var commercial = new Project(Commercial, "COM-002", "Commercial Office Development", "Northgate Properties");
        var infra = new Project(InfrastructureProject, "INF-003", "Infrastructure Development", "City Works Department");

        // R18 demo: estimator1 cannot see INF; viewer1 sees only RES; reviewer1 is not on INF.
        foreach (var u in new[] { Estimator1, Reviewer1, Viewer1, Manager1 }) residential.Assign(u);
        foreach (var u in new[] { Estimator1, Estimator2, Reviewer1, Reviewer2, Manager1 }) commercial.Assign(u);
        foreach (var u in new[] { Estimator2, Reviewer2, Manager1 }) infra.Assign(u);
        db.Projects.AddRange(residential, commercial, infra);

        var jan = new DateOnly(2026, 1, 1);
        var jul = new DateOnly(2026, 7, 1);
        var concrete = new CatalogueItem(Concrete, "CONC-C30", "Concrete Grade C30", "m3");
        concrete.AddRate(G(), jan, M(1450.00m), M(320.00m));
        concrete.AddRate(G(), jul, M(1512.50m), M(335.00m)); // R2 example from the brief
        var rebar = new CatalogueItem(Rebar, "STEEL-REBAR", "Reinforcement Steel", "t");
        rebar.AddRate(G(), jan, M(1180.00m), M(260.00m));
        rebar.AddRate(G(), jul, M(1225.00m), M(270.00m));
        var labour = new CatalogueItem(Labour, "LABOUR-GEN", "General Labour", "hr");
        labour.AddRate(G(), jan, M(45.00m), M(45.00m));
        labour.AddRate(G(), jul, M(47.50m), M(47.50m));
        var brick = new CatalogueItem(Brick, "BRICK-STD", "Standard Brick", "each");
        brick.AddRate(G(), jan, M(0.85m), M(0.30m));
        var excavation = new CatalogueItem(Excavation, "EXCAVATION", "Excavation Work", "m3");
        excavation.AddRate(G(), jan, M(38.00m), M(22.00m));
        db.CatalogueItems.AddRange(concrete, rebar, labour, brick, excavation);

        items = new[] { concrete, rebar, labour, brick, excavation }.ToDictionary(i => i.Id);
    }

    private void SeedEstimates(Dictionary<Guid, CatalogueItem> items)
    {
        var t = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // RES EST-0001 — Draft, priced in March so it uses the January rates.
        var e1 = New(Residential, "EST-0001", "Substructure and frame — Block A", new DateOnly(2026, 3, 15), Estimator1, t);
        Line(e1, items[Concrete], 120.5m, 10m, t);
        Line(e1, items[Rebar], 14.25m, 12.5m, t);
        Line(e1, items[Labour], 320m, 0m, t);

        // RES EST-0002 — Submitted, priced in August so it uses the July rates.
        var e2 = New(Residential, "EST-0002", "External works and paving", new DateOnly(2026, 8, 1), Estimator1, t);
        Line(e2, items[Excavation], 850m, 8m, t);
        Line(e2, items[Concrete], 45m, 10m, t);
        e2.Submit(2, today, Estimator1, t.AddDays(1));

        // RES EST-0003 — Approved (R10: locked).
        var e3 = New(Residential, "EST-0003", "Masonry package", new DateOnly(2026, 2, 1), Estimator1, t);
        Line(e3, items[Brick], 48000m, 15m, t);
        Line(e3, items[Labour], 600m, 5m, t);
        e3.Submit(2, today, Estimator1, t.AddDays(1));
        e3.Approve(Reviewer1, t.AddDays(2));

        // COM EST-0001 — Submitted, created by reviewer1 (while they were an estimator): R16 demo.
        var e4 = New(Commercial, "EST-0001", "Fit-out allowance — Level 3", new DateOnly(2026, 9, 1), Reviewer1, t);
        Line(e4, items[Labour], 1200m, 7.5m, t);
        Line(e4, items[Brick], 9500m, 10m, t);
        e4.Submit(2, today, Reviewer1, t.AddDays(1));

        // COM EST-0002 — Draft with a future pricing date: R9 blocks submission.
        var e5 = New(Commercial, "EST-0002", "Piling and foundations (FY27 pricing)", new DateOnly(2027, 1, 15), Estimator2, t);
        Line(e5, items[Concrete], 310m, 9m, t);
        Line(e5, items[Rebar], 38.5m, 9m, t);

        // COM EST-0003 — Rejected, ready to be reopened.
        var e6 = New(Commercial, "EST-0003", "Basement retaining walls", new DateOnly(2026, 6, 20), Estimator2, t);
        Line(e6, items[Concrete], 95m, 10m, t);
        Line(e6, items[Rebar], 22m, 10m, t);
        e6.Submit(2, today, Estimator2, t.AddDays(1));
        e6.Reject(Reviewer2, "Rebar quantity looks double-counted against the wall schedule.", t.AddDays(2));

        // INF EST-0001 — Draft.
        var e7 = New(InfrastructureProject, "EST-0001", "Drainage and earthworks — Phase 1", new DateOnly(2026, 10, 15), Estimator2, t);
        Line(e7, items[Excavation], 4200.75m, 6m, t);
        Line(e7, items[Labour], 1850m, 0m, t);
        Line(e7, items[Concrete], 60.125m, 8m, t);
    }

    /// <summary>R19 demo: one estimate with N lines (each line needs its own catalogue item, per R7).</summary>
    private async Task SeedLargeEstimateAsync(int lineCount, CancellationToken ct)
    {
        const int batch = 5_000;
        var t = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var estimate = New(InfrastructureProject, "EST-0002", $"Highway package — {lineCount:N0} lines (scale test)",
            new DateOnly(2026, 9, 30), Estimator2, t);
        await db.SaveChangesAsync(ct);

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var random = new Random(42);
        for (var start = 0; start < lineCount; start += batch)
        {
            var count = Math.Min(batch, lineCount - start);
            for (var i = start; i < start + count; i++)
            {
                var item = new CatalogueItem(Guid.NewGuid(), $"BULK-{i:000000}", $"Bulk scale item {i:000000}", "each");
                var rate = random.Next(100, 500_000) / 100m;
                item.AddRate(G(), new DateOnly(2026, 1, 1), M(rate), M(decimal.Round(rate * 0.3m, 2)));
                db.CatalogueItems.Add(item);
                var line = estimate.AddOrAdjustLine(item, null, Quantity.Create(random.Next(1, 100_000) / 10m),
                    MarkupPercentage.Create(random.Next(0, 2000) / 100m), t);
                db.EstimateLines.Add(line);
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            db.Attach(estimate);
            logger.LogInformation("Seeded {Count} large-estimate lines", start + count);
        }
        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }

    private Estimate New(Guid project, string number, string description, DateOnly pricingDate, Guid createdBy, DateTimeOffset at)
    {
        var e = Estimate.Create(G(), project, number, description, pricingDate, createdBy, at);
        db.Estimates.Add(e);
        return e;
    }

    private void Line(Estimate e, CatalogueItem item, decimal qty, decimal markup, DateTimeOffset at) =>
        db.EstimateLines.Add(e.AddOrAdjustLine(item, null, Quantity.Create(qty), MarkupPercentage.Create(markup), at));

    private static Money M(decimal v) => Money.FromExact(v);
    private static Guid G() => Guid.NewGuid();
}
