using HAMBOX.Modules.Legal.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Legal.Infrastructure.Configurations.Legal;

internal sealed class LegalSectionConfiguration : IEntityTypeConfiguration<LegalSection>
{
    public void Configure(EntityTypeBuilder<LegalSection> builder)
    {
        builder.ToTable("LegalSections");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Slug).HasMaxLength(150).IsRequired();
        builder.HasIndex(d => d.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.Property(d => d.Category).HasMaxLength(100);
        builder.Property(d => d.Icon).HasMaxLength(100);
        builder.Property(d => d.DescriptionEn).HasMaxLength(500);
        builder.Property(d => d.DescriptionAr).HasMaxLength(500);
        builder.Property(d => d.SeoTitle).HasMaxLength(200);
        builder.Property(d => d.SeoDescription).HasMaxLength(500);
        builder.Property(d => d.SeoKeywords).HasMaxLength(300);
        builder.Property(d => d.CreatedBy).HasMaxLength(128);
        builder.Property(d => d.ModifiedBy).HasMaxLength(128);
        builder.HasIndex(d => d.IsDeleted).HasFilter("[IsDeleted] = 0");

        builder.HasMany(d => d.Versions)
            .WithOne()
            .HasForeignKey(v => v.LegalSectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(LegalSection.Versions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(d => d.DomainEvents);
    }
}

internal sealed class LegalSectionVersionConfiguration : IEntityTypeConfiguration<LegalSectionVersion>
{
    public void Configure(EntityTypeBuilder<LegalSectionVersion> builder)
    {
        builder.ToTable("LegalSectionVersions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(v => v.TitleAr).HasMaxLength(200);
        builder.Property(v => v.ContentEn).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(v => v.ContentAr).HasColumnType("nvarchar(max)");
        builder.Property(v => v.VersionNotes).HasMaxLength(1000);
        builder.Property(v => v.PublishedBy).HasMaxLength(128);
        builder.HasIndex(v => new { v.LegalSectionId, v.VersionNumber }).IsUnique();
    }
}

internal sealed class LegalSectionAuditLogConfiguration : IEntityTypeConfiguration<LegalSectionAuditLog>
{
    public void Configure(EntityTypeBuilder<LegalSectionAuditLog> builder)
    {
        builder.ToTable("LegalSectionAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<int>();
        builder.Property(l => l.ActorUserId).HasMaxLength(128);
        builder.Property(l => l.DetailsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(l => new { l.LegalSectionId, l.CreatedOnUtc });
    }
}

internal sealed class LegalSectionAcceptanceConfiguration : IEntityTypeConfiguration<LegalSectionAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalSectionAcceptance> builder)
    {
        builder.ToTable("LegalSectionAcceptances");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).HasMaxLength(128).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(a => a.UserAgent).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.Language).HasMaxLength(16).IsRequired();
        builder.HasIndex(a => new { a.UserId, a.LegalSectionId, a.AcceptedAtUtc });
    }
}
