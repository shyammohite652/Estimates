using System.Reflection;
using ConstructionEstimator.Domain.Estimates;

namespace ConstructionEstimator.ArchitectureTests;

/// <summary>
/// Structural enforcement of the dependency rule (DESIGN.md §3). These read the compiled assemblies'
/// references, so adding e.g. an EF Core package or attribute to the Domain fails the build's test run.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Estimate).Assembly;
    private static readonly Assembly Application = typeof(ConstructionEstimator.Application.Estimates.EstimateService).Assembly;
    private static readonly Assembly Infrastructure = typeof(ConstructionEstimator.Infrastructure.DependencyInjection).Assembly;

    private static string[] References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Domain_depends_only_on_the_base_class_library()
    {
        var offending = References(Domain)
            .Where(n => !(n.StartsWith("System", StringComparison.Ordinal) || n is "netstandard" or "mscorlib"))
            .ToArray();
        Assert.True(offending.Length == 0, $"Domain must not reference: {string.Join(", ", offending)}");
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Extensions")]
    [InlineData("ConstructionEstimator.Application")]
    [InlineData("ConstructionEstimator.Infrastructure")]
    [InlineData("ConstructionEstimator.API")]
    public void Domain_does_not_reference(string forbiddenPrefix) =>
        AssertNoReference(Domain, forbiddenPrefix);

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("ConstructionEstimator.Infrastructure")]
    [InlineData("ConstructionEstimator.API")]
    public void Application_does_not_reference(string forbiddenPrefix) =>
        AssertNoReference(Application, forbiddenPrefix);

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("ConstructionEstimator.API")]
    public void Infrastructure_does_not_reference(string forbiddenPrefix) =>
        AssertNoReference(Infrastructure, forbiddenPrefix);

    /// <summary>
    /// Invariants live in methods, so no domain entity may expose a public setter
    /// (otherwise e.g. Estimate.Status could be assigned around the state machine).
    /// </summary>
    [Fact]
    public void Domain_types_expose_no_public_setters()
    {
        var offenders = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsPublic: true } && !typeof(Exception).IsAssignableFrom(t))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToArray();
        Assert.True(offenders.Length == 0, $"Public setters found: {string.Join(", ", offenders)}");
    }

    private static void AssertNoReference(Assembly assembly, string forbiddenPrefix)
    {
        var hits = References(assembly).Where(n => n.StartsWith(forbiddenPrefix, StringComparison.Ordinal)).ToArray();
        Assert.True(hits.Length == 0,
            $"{assembly.GetName().Name} must not depend on {forbiddenPrefix} (found: {string.Join(", ", hits)})");
    }
}
