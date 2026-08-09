using HAMBOX.Modules.Content.Domain.LandingPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Content.Infrastructure.Configurations.LandingPages;

internal sealed class LandingPageTemplateConfiguration : IEntityTypeConfiguration<LandingPageTemplate>
{
    public void Configure(EntityTypeBuilder<LandingPageTemplate> builder)
    {
        builder.ToTable("LandingPageTemplates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(120).IsRequired();
        builder.Property(t => t.SectionsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(t => t.DraftSectionsJson).HasColumnType("nvarchar(max)");
        builder.Property(t => t.Scope).HasConversion<int>().IsRequired();
        builder.Property(t => t.SeoTitle).HasMaxLength(200);
        builder.Property(t => t.SeoDescription).HasMaxLength(500);
        builder.Property(t => t.SeoOgImageUrl).HasMaxLength(1000);
        builder.Property(t => t.DraftSeoTitle).HasMaxLength(200);
        builder.Property(t => t.DraftSeoDescription).HasMaxLength(500);
        builder.Property(t => t.DraftSeoOgImageUrl).HasMaxLength(1000);
        builder.Property(t => t.CreatedBy).HasMaxLength(128);
        builder.Property(t => t.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(t => t.IsActive);

        // Enforces "at most one active page per (Scope, TargetId)" at the DB level — for Homepage,
        // TargetId is always null so this covers the single-active-homepage invariant too (previously
        // only enforced by ActivateLandingPageTemplateCommandHandler at the application layer).
        builder.HasIndex(t => new { t.Scope, t.TargetId })
            .IsUnique()
            .HasDatabaseName("IX_LandingPageTemplates_ActiveTarget")
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");

        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class LandingPageAuditLogConfiguration : IEntityTypeConfiguration<LandingPageAuditLog>
{
    public void Configure(EntityTypeBuilder<LandingPageAuditLog> builder)
    {
        builder.ToTable("LandingPageAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<int>();
        builder.Property(l => l.ActorUserId).HasMaxLength(128);
        builder.Property(l => l.DetailsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(l => new { l.TemplateId, l.CreatedOnUtc });
    }
}
