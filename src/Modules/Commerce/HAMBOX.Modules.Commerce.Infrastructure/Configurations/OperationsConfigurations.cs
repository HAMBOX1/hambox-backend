using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class OperationalJobConfiguration : IEntityTypeConfiguration<OperationalJob>
{
    public void Configure(EntityTypeBuilder<OperationalJob> builder)
    {
        builder.ToTable("OperationalJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JobType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PayloadJson);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Queue).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkerId).HasMaxLength(128);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(128);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(128);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.ExceptionDetails).HasMaxLength(4000);
        builder.HasIndex(x => new { x.Status, x.NextVisibleOnUtc, x.Priority });
        builder.HasIndex(x => x.CreatedOnUtc);
        builder.HasIndex(x => x.JobType);
        builder.HasIndex(x => x.Queue);
    }
}

internal sealed class BackgroundJobExecutionHistoryConfiguration : IEntityTypeConfiguration<BackgroundJobExecutionHistory>
{
    public void Configure(EntityTypeBuilder<BackgroundJobExecutionHistory> builder)
    {
        builder.ToTable("BackgroundJobExecutionHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Exception).HasMaxLength(4000);
        builder.Property(x => x.WorkerId).HasMaxLength(128);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.HasIndex(x => new { x.JobId, x.AttemptNumber });
        builder.HasIndex(x => x.StartedOnUtc);
    }
}

internal sealed class ApiRequestLogConfiguration : IEntityTypeConfiguration<ApiRequestLog>
{
    public void Configure(EntityTypeBuilder<ApiRequestLog> builder)
    {
        builder.ToTable("ApiRequestLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Method).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(512).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.UserId).HasMaxLength(64);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.Exception).HasMaxLength(4000);
        builder.HasIndex(x => x.TimestampUtc);
        builder.HasIndex(x => x.StatusCode);
        builder.HasIndex(x => x.Path);
    }
}

internal sealed class OperationalAuditLogConfiguration : IEntityTypeConfiguration<OperationalAuditLog>
{
    public void Configure(EntityTypeBuilder<OperationalAuditLog> builder)
    {
        builder.ToTable("OperationalAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.EntityId).HasMaxLength(128);
        builder.Property(x => x.ActorUserId).HasMaxLength(64);
        builder.Property(x => x.ActorDisplayName).HasMaxLength(256);
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.HasIndex(x => x.OccurredOnUtc);
    }
}

internal sealed class OperationalAlertConfiguration : IEntityTypeConfiguration<OperationalAlert>
{
    public void Configure(EntityTypeBuilder<OperationalAlert> builder)
    {
        builder.ToTable("OperationalAlerts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(128);
        builder.Property(x => x.AcknowledgedByUserId).HasMaxLength(64);
        builder.HasIndex(x => new { x.IsAcknowledged, x.CreatedOnUtc });
        builder.HasIndex(x => x.Code);
    }
}
