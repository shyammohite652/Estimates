using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Infrastructure.Persistence;
using ConstructionEstimator.Infrastructure.Persistence.Queries;
using ConstructionEstimator.Infrastructure.Persistence.Repositories;
using ConstructionEstimator.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionEstimator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.Equals(configuration["Database:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            // Quick-look mode for machines without SQL Server. NOT the real persistence: no unique indexes,
            // no rowversion, data lost on restart. Domain and application rules still apply in full.
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("ConstructionEstimator"));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string 'Default' is not configured.");
            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(3);
                sql.CommandTimeout(60);
            }));
        }

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEstimateRepository, EstimateRepository>();
        services.AddScoped<ICatalogueRepository, CatalogueRepository>();
        services.AddScoped<IProjectAccessRepository, ProjectAccessRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEstimateQueries, EstimateQueries>();
        services.AddScoped<IProjectQueries, ProjectQueries>();
        services.AddScoped<ICatalogueQueries, CatalogueQueries>();
        services.AddScoped<DbSeeder>();
        return services;
    }
}
