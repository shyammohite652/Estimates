using ConstructionEstimator.Application.Auth;
using ConstructionEstimator.Application.Catalogue;
using ConstructionEstimator.Application.Estimates;
using ConstructionEstimator.Application.Projects;
using ConstructionEstimator.Application.Security;

namespace ConstructionEstimator.Application;

/// <summary>Plain registration list; kept free of Microsoft.Extensions.DependencyInjection so the layer stays framework-free.</summary>
public static class ApplicationServices
{
    public static IReadOnlyList<Type> Scoped { get; } =
    [
        typeof(AccessGuard),
        typeof(EstimateService),
        typeof(ProjectService),
        typeof(CatalogueService),
        typeof(AuthService),
    ];
}
