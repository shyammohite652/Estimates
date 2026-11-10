using ConstructionEstimator.Application.Catalogue;
using ConstructionEstimator.Application.Common;
using ConstructionEstimator.Application.Estimates;
using ConstructionEstimator.Application.Projects;
using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Users;

namespace ConstructionEstimator.Application.Abstractions;

/// <summary>The authenticated caller, resolved from the token by the API layer.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    UserRole Role { get; }
}

/// <summary>One use case = one SaveChanges = one database transaction.</summary>
public interface IUnitOfWork
{
    /// <exception cref="ConflictException">On a concurrency or uniqueness conflict.</exception>
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Forget tracked changes after a failed save so state can be re-read.</summary>
    void DiscardChanges();
}

/// <summary>Write-side access to estimates. Line access is single-row by design (R19).</summary>
public interface IEstimateRepository
{
    Task<Estimate?> GetAsync(Guid id, CancellationToken ct);
    Task<EstimateLine?> GetLineAsync(Guid estimateId, long lineId, CancellationToken ct);
    Task<EstimateLine?> GetLineForItemAsync(Guid estimateId, Guid catalogueItemId, CancellationToken ct);
    Task<int> CountLinesAsync(Guid estimateId, CancellationToken ct);
    Task<int> CountEstimatesInProjectAsync(Guid projectId, CancellationToken ct);
    void Add(Estimate estimate);
    void AddLine(EstimateLine line);
    void RemoveLine(EstimateLine line);
}

public interface ICatalogueRepository
{
    /// <summary>Loads the item with all its rates (a handful of rows per item).</summary>
    Task<CatalogueItem?> GetWithRatesAsync(Guid id, CancellationToken ct);
}

public interface IProjectAccessRepository
{
    Task<bool> IsAssignedAsync(Guid userId, Guid projectId, CancellationToken ct);
}

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);
}

/// <summary>
/// Read side: projections straight to DTOs, no tracking. <c>includeLabour</c> controls whether labour
/// columns are even selected (R17) — data that must not be seen is never read.
/// </summary>
public interface IEstimateQueries
{
    Task<EstimateHeaderData?> GetHeaderAsync(Guid estimateId, CancellationToken ct);
    Task<EstimateStatus?> GetStatusAsync(Guid estimateId, CancellationToken ct);
    Task<PagedResult<EstimateLineDto>> GetLinesAsync(Guid estimateId, PageRequest page, bool includeLabour, CancellationToken ct);
    Task<EstimateLineDto?> GetLineAsync(Guid estimateId, long lineId, bool includeLabour, CancellationToken ct);
    Task<IReadOnlyList<EstimateHistoryDto>> GetHistoryAsync(Guid estimateId, CancellationToken ct);
}

public interface IProjectQueries
{
    Task<IReadOnlyList<ProjectSummaryDto>> GetProjectsForUserAsync(Guid userId, CancellationToken ct);
}

public interface ICatalogueQueries
{
    /// <summary>Items matching <paramref name="search"/> (code or description), at most <paramref name="take"/>, priced on the date.</summary>
    Task<IReadOnlyList<CatalogueItemDto>> GetPricedOnAsync(DateOnly pricingDate, string? search, int take, bool includeLabour, CancellationToken ct);
}

public interface ITokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(User user);
}
