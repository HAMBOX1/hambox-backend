using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class SecurityEventLogConfiguration : IEntityTypeConfiguration<SecurityEventLog>
{
    public void Configure(EntityTypeBuilder<SecurityEventLog> builder)
    {
        builder.ToTable("SecurityEventLogs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Severity).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.ActorUserId);
        builder.Property(e => e.TargetUserId);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.Country).HasMaxLength(2);
        builder.Property(e => e.City).HasMaxLength(128);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.OccurredOnUtc).IsRequired();
        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Enums.SecurityEventStatus.Open);
        builder.Property(e => e.AcknowledgedByUserId);
        builder.Property(e => e.AcknowledgedOnUtc);
        builder.Property(e => e.ResolvedByUserId);
        builder.Property(e => e.ResolvedOnUtc);
        builder.Property(e => e.ResolutionNotes).HasMaxLength(2000);
        builder.Property(e => e.CreatedOnUtc).IsRequired();
        builder.Property(e => e.ModifiedOnUtc);

        builder.HasIndex(e => e.OccurredOnUtc).HasDatabaseName("IX_SecurityEventLogs_OccurredOnUtc");
        builder.HasIndex(e => e.EventType).HasDatabaseName("IX_SecurityEventLogs_EventType");
        builder.HasIndex(e => e.TargetUserId).HasDatabaseName("IX_SecurityEventLogs_TargetUserId");
        builder.HasIndex(e => new { e.Status, e.Severity }).HasDatabaseName("IX_SecurityEventLogs_Status_Severity");
    }
}
