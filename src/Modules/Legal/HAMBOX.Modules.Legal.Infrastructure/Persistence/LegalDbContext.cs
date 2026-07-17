using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Domain.Legal;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Legal.Infrastructure.Persistence;

public sealed class LegalDbContext(DbContextOptions<LegalDbContext> options)
    : DbContext(options), ILegalDbContext
{
    public DbSet<LegalSection> LegalSections => Set<LegalSection>();
    public DbSet<LegalSectionVersion> LegalSectionVersions => Set<LegalSectionVersion>();
    public DbSet<LegalSectionAuditLog> LegalSectionAuditLogs => Set<LegalSectionAuditLog>();
    public DbSet<LegalSectionAcceptance> LegalSectionAcceptances => Set<LegalSectionAcceptance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("platform");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LegalDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for soft-deletable entities (mirrors CatalogDbContext's copy
    /// of this helper — soft-delete filter registration is duplicated per module context by
    /// existing convention, not factored into a shared base).
    /// </summary>
    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var condition = System.Linq.Expressions.Expression.Equal(
                property,
                System.Linq.Expressions.Expression.Constant(false));
            var lambda = System.Linq.Expressions.Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
