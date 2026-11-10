using ConstructionEstimator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructionEstimator.ArchitectureTests;

/// <summary>
/// The schema is part of rule enforcement (R7 unique index, R12 filtered index, money precision).
/// This fails if anyone changes the EF model without adding a migration — no database needed.
/// </summary>
public class MigrationTests
{
    private static AppDbContext Context() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused;Database=unused").Options);

    [Fact]
    public void Model_has_no_changes_missing_from_migrations()
    {
        using var db = Context();
        Assert.False(db.Database.HasPendingModelChanges(),
            "The EF model differs from the latest migration snapshot. Add a migration.");
    }

    [Theory]
    [InlineData("CREATE UNIQUE INDEX [IX_EstimateLines_EstimateId_CatalogueItemId] ON [EstimateLines] ([EstimateId], [CatalogueItemId])")]
    [InlineData("CREATE UNIQUE INDEX [UX_EstimateHistory_SingleApproval] ON [EstimateHistory] ([EstimateId]) WHERE [Action] = 'Approved'")]
    [InlineData("[Quantity] decimal(18,3) NOT NULL")]
    [InlineData("[MarkupPercent] decimal(5,2) NOT NULL")]
    [InlineData("[LineTotal] decimal(18,2) NOT NULL")]
    [InlineData("[RowVersion] rowversion")]
    public void Schema_contains_rule_enforcing_constraints(string expectedSql)
    {
        using var db = Context();
        var script = db.Database.GenerateCreateScript();
        Assert.Contains(expectedSql, script);
    }
}
