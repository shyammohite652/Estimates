using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ConstructionEstimator.Infrastructure.Persistence;

/// <summary>Translates persistence conflicts into one application exception, in one place.</summary>
internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("The estimate was changed by someone else. Reload and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new ConflictException("A conflicting change was saved at the same time. Reload and try again.", ex);
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();
}
