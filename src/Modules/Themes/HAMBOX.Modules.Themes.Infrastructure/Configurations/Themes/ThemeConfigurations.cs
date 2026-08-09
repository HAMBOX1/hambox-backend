using HAMBOX.Modules.Themes.Domain.Themes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Themes.Infrastructure.Configurations.Themes;

internal sealed class StoreThemeConfiguration : IEntityTypeConfiguration<StoreTheme>
{
    public void Configure(EntityTypeBuilder<StoreTheme> builder)
    {
        builder.ToTable("StoreThemes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.BaseMode).HasConversion<int>();
        builder.HasIndex(t => t.Slug).IsUnique();

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        builder.HasMany(t => t.Versions)
            .WithOne()
            .HasForeignKey(v => v.ThemeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Schedules)
            .WithOne()
            .HasForeignKey(s => s.ThemeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Assignments)
            .WithOne()
            .HasForeignKey(a => a.ThemeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Assets)
            .WithOne()
            .HasForeignKey(a => a.ThemeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(StoreTheme.Versions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(StoreTheme.Schedules))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(StoreTheme.Assignments))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(StoreTheme.Assets))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class ThemeVersionConfiguration : IEntityTypeConfiguration<ThemeVersion>
{
    public void Configure(EntityTypeBuilder<ThemeVersion> builder)
    {
        builder.ToTable("ThemeVersions");
        builder.HasKey(v => v.Id);
        // No explicit HasColumnType: an un-annotated, un-length-capped string already defaults to
        // nvarchar(max) under the SqlServer provider (same DDL either way) — leaving it implicit
        // instead of hardcoding a SQL-Server-only type string keeps this portable to other
        // providers (e.g. SQLite, used by integration tests elsewhere in this solution).
        builder.Property(v => v.TokensJson).IsRequired();
        builder.Property(v => v.Notes).HasMaxLength(2000);
        builder.Property(v => v.PublishedBy).HasMaxLength(128);
        builder.Property(v => v.ValidationWarningsJson);
        builder.HasIndex(v => new { v.ThemeId, v.VersionNumber }).IsUnique();

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();
    }
}

internal sealed class ThemeScheduleConfiguration : IEntityTypeConfiguration<ThemeSchedule>
{
    public void Configure(EntityTypeBuilder<ThemeSchedule> builder)
    {
        builder.ToTable("ThemeSchedules");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.RecurrenceRule).HasMaxLength(500);
    }
}

internal sealed class ThemeAssignmentConfiguration : IEntityTypeConfiguration<ThemeAssignment>
{
    public void Configure(EntityTypeBuilder<ThemeAssignment> builder)
    {
        builder.ToTable("ThemeAssignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AssignmentType).HasConversion<int>();
        builder.Property(a => a.TargetKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(a => new { a.AssignmentType, a.TargetKey, a.IsActive });
    }
}

internal sealed class ThemeAssetConfiguration : IEntityTypeConfiguration<ThemeAsset>
{
    public void Configure(EntityTypeBuilder<ThemeAsset> builder)
    {
        builder.ToTable("ThemeAssets");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AssetType).HasConversion<int>();
        builder.Property(a => a.Url).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.AltText).HasMaxLength(500);
        builder.HasIndex(a => new { a.ThemeId, a.AssetType }).IsUnique();
    }
}

internal sealed class ThemeAuditLogConfiguration : IEntityTypeConfiguration<ThemeAuditLog>
{
    public void Configure(EntityTypeBuilder<ThemeAuditLog> builder)
    {
        builder.ToTable("ThemeAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<int>();
        builder.Property(l => l.ActorUserId).HasMaxLength(128);
        builder.Property(l => l.DetailsJson);
        builder.HasIndex(l => new { l.ThemeId, l.CreatedOnUtc });
    }
}

internal sealed class ThemePreviewSessionConfiguration : IEntityTypeConfiguration<ThemePreviewSession>
{
    public void Configure(EntityTypeBuilder<ThemePreviewSession> builder)
    {
        builder.ToTable("ThemePreviewSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Token).HasMaxLength(128).IsRequired();
        builder.Property(s => s.CreatedByUserId).HasMaxLength(128);
        builder.HasIndex(s => s.Token).IsUnique();
    }
}
