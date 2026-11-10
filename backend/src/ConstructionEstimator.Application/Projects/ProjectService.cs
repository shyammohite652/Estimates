using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Domain.Estimates;

namespace ConstructionEstimator.Application.Projects;

public sealed record EstimateSummaryDto(Guid Id, string Number, string Description, EstimateStatus Status,
    DateOnly PricingDate, int LineCount, decimal Total);

public sealed record ProjectSummaryDto(Guid Id, string Code, string Name, string? Client,
    IReadOnlyList<EstimateSummaryDto> Estimates);

public sealed class ProjectService(ICurrentUser currentUser, IProjectQueries queries)
{
    /// <summary>R18: only projects the caller is assigned to.</summary>
    public Task<IReadOnlyList<ProjectSummaryDto>> GetMyProjectsAsync(CancellationToken ct) =>
        queries.GetProjectsForUserAsync(currentUser.UserId, ct);
}
