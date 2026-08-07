using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="ReferralAuditLog"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ReferralAuditLogConfiguration : IEntityTypeConfiguration<ReferralAuditLog>
{
    public void Configure(EntityTypeBuilder<ReferralAuditLog> builder)
    {
        builder.ToTable("ReferralAuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ReferrerUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.PerformedByUserId).HasMaxLength(450);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.OccurredOnUtc).IsRequired();

        builder.HasIndex(a => a.ReferralHistoryEntryId).HasDatabaseName("IX_ReferralAuditLogs_ReferralHistoryEntryId");
        builder.HasIndex(a => a.ReferrerUserId).HasDatabaseName("IX_ReferralAuditLogs_ReferrerUserId");
        builder.HasIndex(a => a.OccurredOnUtc).HasDatabaseName("IX_ReferralAuditLogs_OccurredOnUtc");
    }
}
