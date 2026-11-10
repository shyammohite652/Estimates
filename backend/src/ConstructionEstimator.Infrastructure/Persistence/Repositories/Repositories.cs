using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ConstructionEstimator.Infrastructure.Persistence.Repositories;

internal sealed class EstimateRepository(AppDbContext db) : IEstimateRepository
{
    // History is not loaded for commands: new entries are appended and inserted, existing ones are never touched.
    public Task<Estimate?> GetAsync(Guid id, CancellationToken ct) =>
        db.Estimates.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<EstimateLine?> GetLineAsync(Guid estimateId, long lineId, CancellationToken ct) =>
        db.EstimateLines.FirstOrDefaultAsync(l => l.EstimateId == estimateId && l.Id == lineId, ct);

    public Task<EstimateLine?> GetLineForItemAsync(Guid estimateId, Guid catalogueItemId, CancellationToken ct) =>
        db.EstimateLines.FirstOrDefaultAsync(l => l.EstimateId == estimateId && l.CatalogueItemId == catalogueItemId, ct);

    public Task<int> CountLinesAsync(Guid estimateId, CancellationToken ct) =>
        db.EstimateLines.CountAsync(l => l.EstimateId == estimateId, ct);

    public Task<int> CountEstimatesInProjectAsync(Guid projectId, CancellationToken ct) =>
        db.Estimates.CountAsync(e => e.ProjectId == projectId, ct);

    public void Add(Estimate estimate) => db.Estimates.Add(estimate);
    public void AddLine(EstimateLine line) => db.EstimateLines.Add(line);
    public void RemoveLine(EstimateLine line) => db.EstimateLines.Remove(line);
}

internal sealed class CatalogueRepository(AppDbContext db) : ICatalogueRepository
{
    public Task<CatalogueItem?> GetWithRatesAsync(Guid id, CancellationToken ct) =>
        db.CatalogueItems.AsNoTracking().Include(i => i.Rates).FirstOrDefaultAsync(i => i.Id == id, ct);
}

internal sealed class ProjectAccessRepository(AppDbContext db) : IProjectAccessRepository
{
    public Task<bool> IsAssignedAsync(Guid userId, Guid projectId, CancellationToken ct) =>
        db.ProjectAssignments.AnyAsync(a => a.UserId == userId && a.ProjectId == projectId, ct);
}

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.Role).ThenBy(u => u.Email).ToListAsync(ct);
}
