using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Projects;
using ConstructionEstimator.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ConstructionEstimator.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();
    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();
    public DbSet<CatalogueRate> CatalogueRates => Set<CatalogueRate>();
    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<EstimateLine> EstimateLines => Set<EstimateLine>();
    public DbSet<EstimateHistoryEntry> EstimateHistory => Set<EstimateHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
