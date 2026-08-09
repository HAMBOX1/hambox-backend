using HAMBOX.Modules.Themes.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Themes.Infrastructure.Configurations.Campaigns;

internal sealed class ThemeCampaignConfiguration : IEntityTypeConfiguration<ThemeCampaign>
{
    public void Configure(EntityTypeBuilder<ThemeCampaign> builder)
    {
        builder.ToTable("ThemeCampaigns");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Status).HasConversion<int>();

        // Restrict, not Cascade: a theme being cleaned up should never silently erase campaign
        // history. A dangling ThemeId (theme later soft-deleted) is handled gracefully at
        // resolution time by the same fallthrough Schedule/Store already rely on — see ThemeEngine.
        builder.HasOne<HAMBOX.Modules.Themes.Domain.Themes.StoreTheme>()
            .WithMany()
            .HasForeignKey(c => c.ThemeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Resolver query shape: Status + IsEnabled + StartsAtUtc/EndsAtUtc window, same access
        // pattern as ThemeSchedules — one composite index covers it.
        builder.HasIndex(c => new { c.Status, c.IsEnabled, c.StartsAtUtc, c.EndsAtUtc });
        builder.HasIndex(c => c.ThemeId);

        builder.Ignore(c => c.DomainEvents);

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();
    }
}

internal sealed class CampaignAuditLogConfiguration : IEntityTypeConfiguration<CampaignAuditLog>
{
    public void Configure(EntityTypeBuilder<CampaignAuditLog> builder)
    {
        builder.ToTable("CampaignAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<int>();
        builder.Property(l => l.ActorUserId).HasMaxLength(128);
        // No explicit HasColumnType — see the identical comment on ThemeAuditLogConfiguration.
        builder.Property(l => l.DetailsJson);
        builder.HasIndex(l => new { l.CampaignId, l.CreatedOnUtc });
    }
}
