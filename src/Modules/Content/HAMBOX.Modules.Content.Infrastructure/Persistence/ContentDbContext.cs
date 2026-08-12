using System.Linq.Expressions;
using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.Modules.Content.Domain.LandingPages;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Infrastructure.Persistence;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : DbContext(options), IContentDbContext
{
    public DbSet<LandingPageTemplate> LandingPageTemplates => Set<LandingPageTemplate>();
    public DbSet<LandingPageAuditLog> LandingPageAuditLogs => Set<LandingPageAuditLog>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<FaqCategory> FaqCategories => Set<FaqCategory>();
    public DbSet<FaqAuditLog> FaqAuditLogs => Set<FaqAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("content");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    // Mirrors SuppliersDbContext's reflective soft-delete filter registration (see CLAUDE.md §3/§13 —
    // this per-context duplication is a known, accepted pattern, not factored into a shared base yet).
    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
