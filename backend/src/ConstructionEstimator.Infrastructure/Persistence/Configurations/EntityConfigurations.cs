using ConstructionEstimator.Domain.Catalogue;
using ConstructionEstimator.Domain.Estimates;
using ConstructionEstimator.Domain.Projects;
using ConstructionEstimator.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionEstimator.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        b.HasIndex(x => x.Email).IsUnique();
    }
}

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Client).HasMaxLength(200);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasMany(x => x.Assignments).WithOne().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProjectAssignmentConfiguration : IEntityTypeConfiguration<ProjectAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectAssignment> b)
    {
        b.ToTable("ProjectAssignments");
        b.HasKey(x => new { x.ProjectId, x.UserId });
        // R18 lookups go by user: "which projects can I see?"
        b.HasIndex(x => x.UserId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CatalogueItemConfiguration : IEntityTypeConfiguration<CatalogueItem>
{
    public void Configure(EntityTypeBuilder<CatalogueItem> b)
    {
        b.ToTable("CatalogueItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasMany(x => x.Rates).WithOne().HasForeignKey(r => r.CatalogueItemId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Rates).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CatalogueRateConfiguration : IEntityTypeConfiguration<CatalogueRate>
{
    public void Configure(EntityTypeBuilder<CatalogueRate> b)
    {
        b.ToTable("CatalogueRates", t =>
        {
            t.HasCheckConstraint("CK_CatalogueRates_Labour", "[LabourComponent] >= 0 AND [LabourComponent] <= [Rate]");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Rate).HasPrecision(18, 2);
        b.Property(x => x.LabourComponent).HasPrecision(18, 2);
        // R2: one rate per item per effective date; also the index used to find the rate on a date.
        b.HasIndex(x => new { x.CatalogueItemId, x.EffectiveFrom }).IsUnique();
    }
}

internal sealed class EstimateConfiguration : IEntityTypeConfiguration<Estimate>
{
    public const string RowVersion = "RowVersion";

    public void Configure(EntityTypeBuilder<Estimate> b)
    {
        b.ToTable("Estimates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(32).IsRequired();
        b.Property(x => x.Description).HasMaxLength(Estimate.DescriptionMaxLength).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        // Optimistic concurrency: any change (including line edits, which touch UpdatedAt) moves the token.
        b.Property<byte[]>(RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.History).WithOne().HasForeignKey(h => h.EstimateId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class EstimateLineConfiguration : IEntityTypeConfiguration<EstimateLine>
{
    public void Configure(EntityTypeBuilder<EstimateLine> b)
    {
        b.ToTable("EstimateLines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityColumn(); // monotonic clustered key: cheap inserts, natural paging order
        b.Property(x => x.ItemCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.ItemDescription).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(16).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.MarkupPercent).HasPrecision(5, 2);
        b.Property(x => x.UnitRate).HasPrecision(18, 2);
        b.Property(x => x.UnitLabourCost).HasPrecision(18, 2);
        b.Property(x => x.NetAmount).HasPrecision(18, 2);
        b.Property(x => x.MarkupAmount).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.Property(x => x.LabourAmount).HasPrecision(18, 2);

        // R7: a catalogue item at most once per estimate — holds even under concurrent requests.
        b.HasIndex(x => new { x.EstimateId, x.CatalogueItemId }).IsUnique();
        // R5/R19: (EstimateId, Id) ordering for paging, covering the amounts so totals are an index-only SUM.
        b.HasIndex(x => x.EstimateId)
            .HasDatabaseName("IX_EstimateLines_EstimateId_Totals")
            .IncludeProperties(x => new { x.NetAmount, x.MarkupAmount, x.LineTotal, x.LabourAmount });

        b.HasOne<Estimate>().WithMany().HasForeignKey(x => x.EstimateId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<CatalogueItem>().WithMany().HasForeignKey(x => x.CatalogueItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EstimateHistoryConfiguration : IEntityTypeConfiguration<EstimateHistoryEntry>
{
    public void Configure(EntityTypeBuilder<EstimateHistoryEntry> b)
    {
        b.ToTable("EstimateHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityColumn();
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.HasIndex(x => new { x.EstimateId, x.OccurredAt });
        // R12 backstop: the database refuses a second approval record for the same estimate.
        b.HasIndex(x => x.EstimateId)
            .IsUnique()
            .HasFilter("[Action] = 'Approved'")
            .HasDatabaseName("UX_EstimateHistory_SingleApproval");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
