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
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.OccurredOnUtc).IsRequired();
        builder.Property(e => e.CreatedOnUtc).IsRequired();
        builder.Property(e => e.ModifiedOnUtc);

        builder.HasIndex(e => e.OccurredOnUtc).HasDatabaseName("IX_SecurityEventLogs_OccurredOnUtc");
        builder.HasIndex(e => e.EventType).HasDatabaseName("IX_SecurityEventLogs_EventType");
        builder.HasIndex(e => e.TargetUserId).HasDatabaseName("IX_SecurityEventLogs_TargetUserId");
    }
}
