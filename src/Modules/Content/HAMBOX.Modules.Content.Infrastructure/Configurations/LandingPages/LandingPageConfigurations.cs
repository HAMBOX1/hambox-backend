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
        builder.Property(t => t.CreatedBy).HasMaxLength(128);
        builder.Property(t => t.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(t => t.IsActive);

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
