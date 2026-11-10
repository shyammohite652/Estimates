using System.Xml.Linq;

namespace ConstructionEstimator.ArchitectureTests;

/// <summary>
/// Assembly-reference checks only see packages the code actually uses. This closes the gap: the Domain
/// project file itself may not declare any package or framework reference.
/// </summary>
public class ProjectFileTests
{
    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ConstructionEstimator.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate ConstructionEstimator.sln");
    }

    private static XDocument Project(string name) =>
        XDocument.Load(Path.Combine(BackendRoot(), "src", name, $"{name}.csproj"));

    [Fact]
    public void Domain_project_declares_no_package_framework_or_project_references()
    {
        var doc = Project("ConstructionEstimator.Domain");
        var refs = doc.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "FrameworkReference" or "ProjectReference")
            .Select(e => $"{e.Name.LocalName}:{e.Attribute("Include")?.Value}")
            .ToArray();
        Assert.True(refs.Length == 0, $"Domain must stay dependency-free, found {string.Join(", ", refs)}");
    }

    [Fact]
    public void Application_project_references_only_the_domain()
    {
        var doc = Project("ConstructionEstimator.Application");
        var refs = doc.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "FrameworkReference" or "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .ToArray();
        Assert.All(refs, r => Assert.Contains("ConstructionEstimator.Domain", r));
    }
}
