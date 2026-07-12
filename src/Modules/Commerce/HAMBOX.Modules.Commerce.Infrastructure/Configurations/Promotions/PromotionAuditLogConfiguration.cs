using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Promotions;

internal sealed class PromotionAuditLogConfiguration : IEntityTypeConfiguration<PromotionAuditLog>
{
    public void Configure(EntityTypeBuilder<PromotionAuditLog> builder)
    {
        builder.ToTable("PromotionAuditLogs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.PerformedByUserId).HasMaxLength(450);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.OccurredOnUtc).IsRequired();

        builder.HasIndex(a => a.PromotionId).HasDatabaseName("IX_PromotionAuditLogs_PromotionId");
        builder.HasIndex(a => a.OccurredOnUtc).HasDatabaseName("IX_PromotionAuditLogs_OccurredOnUtc");
    }
}
