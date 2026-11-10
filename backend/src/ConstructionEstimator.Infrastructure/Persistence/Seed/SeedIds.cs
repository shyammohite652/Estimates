namespace ConstructionEstimator.Infrastructure.Persistence.Seed;

/// <summary>Stable ids for development seed data (also used by docs and manual testing).</summary>
public static class SeedIds
{
    public static readonly Guid Estimator1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid Estimator2 = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid Reviewer1 = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid Reviewer2 = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid Viewer1 = Guid.Parse("10000000-0000-0000-0000-000000000005");
    public static readonly Guid Manager1 = Guid.Parse("10000000-0000-0000-0000-000000000006");

    public static readonly Guid Residential = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid Commercial = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid InfrastructureProject = Guid.Parse("20000000-0000-0000-0000-000000000003");

    public static readonly Guid Concrete = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid Rebar = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid Labour = Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid Brick = Guid.Parse("30000000-0000-0000-0000-000000000004");
    public static readonly Guid Excavation = Guid.Parse("30000000-0000-0000-0000-000000000005");
}
