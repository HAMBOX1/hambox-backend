using HAMBOX.Modules.Identity.Domain.PlatformSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class PlatformSettingsCategoryConfiguration : IEntityTypeConfiguration<PlatformSettingsCategory>
{
    public void Configure(EntityTypeBuilder<PlatformSettingsCategory> builder)
    {
        builder.ToTable("PlatformSettingsCategories");

        builder.HasKey(x => x.CategoryKey);

        builder.Property(x => x.CategoryKey).HasMaxLength(64);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.ModifiedOnUtc).IsRequired();
        builder.Property(x => x.ModifiedByUserId).HasMaxLength(64);
    }
}

internal sealed class PlatformSettingsAuditLogConfiguration : IEntityTypeConfiguration<PlatformSettingsAuditLog>
{
    public void Configure(EntityTypeBuilder<PlatformSettingsAuditLog> builder)
    {
        builder.ToTable("PlatformSettingsAuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CategoryKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ActorUserId).HasMaxLength(64);
        builder.Property(x => x.ActorDisplayName).HasMaxLength(256);
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.Property(x => x.OccurredOnUtc).IsRequired();

        builder.HasIndex(x => x.OccurredOnUtc);
        builder.HasIndex(x => x.CategoryKey);
    }
}
