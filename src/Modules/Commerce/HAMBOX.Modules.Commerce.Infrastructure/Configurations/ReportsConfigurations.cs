using HAMBOX.Modules.Commerce.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("ReportDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReportType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FiltersJson);
        builder.Property(x => x.FormatDefault).HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(64);
        builder.HasIndex(x => x.ReportType);
        builder.HasIndex(x => x.CreatedByUserId);
        builder.HasIndex(x => x.CreatedOnUtc);
    }
}

internal sealed class ReportFavoriteConfiguration : IEntityTypeConfiguration<ReportFavorite>
{
    public void Configure(EntityTypeBuilder<ReportFavorite> builder)
    {
        builder.ToTable("ReportFavorites");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.ReportDefinitionId }).IsUnique();
        builder.HasIndex(x => x.CreatedOnUtc);
    }
}

internal sealed class ReportDownloadConfiguration : IEntityTypeConfiguration<ReportDownload>
{
    public void Configure(EntityTypeBuilder<ReportDownload> builder)
    {
        builder.ToTable("ReportDownloads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReportType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(16).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedOnUtc);
    }
}

internal sealed class ScheduledReportConfiguration : IEntityTypeConfiguration<ScheduledReport>
{
    public void Configure(EntityTypeBuilder<ScheduledReport> builder)
    {
        builder.ToTable("ScheduledReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReportType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Frequency).HasMaxLength(32).IsRequired();
        builder.Property(x => x.EmailRecipientsJson).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(64);
        builder.HasIndex(x => new { x.IsEnabled, x.NextRunOnUtc });
        builder.HasIndex(x => x.CreatedOnUtc);
    }
}

internal sealed class ScheduledReportExecutionConfiguration : IEntityTypeConfiguration<ScheduledReportExecution>
{
    public void Configure(EntityTypeBuilder<ScheduledReportExecution> builder)
    {
        builder.ToTable("ScheduledReportExecutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TriggeredBy).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(2000);
        builder.HasIndex(x => new { x.ScheduledReportId, x.StartedOnUtc });
        builder.HasIndex(x => x.Status);
    }
}
