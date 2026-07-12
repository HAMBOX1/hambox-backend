using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class OrderAuditEntryConfiguration : IEntityTypeConfiguration<OrderAuditEntry>
{
    public void Configure(EntityTypeBuilder<OrderAuditEntry> builder)
    {
        builder.ToTable("OrderAuditEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ActorUserId).HasMaxLength(450);
        builder.Property(e => e.ActorDisplayName).HasMaxLength(200);

        builder.HasIndex(e => e.OrderId).HasDatabaseName("IX_OrderAuditEntries_OrderId");
        builder.HasIndex(e => e.OccurredOnUtc).HasDatabaseName("IX_OrderAuditEntries_OccurredOnUtc");
    }
}
