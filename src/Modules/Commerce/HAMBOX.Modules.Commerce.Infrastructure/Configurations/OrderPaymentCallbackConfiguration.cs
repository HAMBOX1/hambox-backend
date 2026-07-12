using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class OrderPaymentCallbackConfiguration : IEntityTypeConfiguration<OrderPaymentCallback>
{
    public void Configure(EntityTypeBuilder<OrderPaymentCallback> builder)
    {
        builder.ToTable("OrderPaymentCallbacks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider).HasMaxLength(64).IsRequired();
        builder.Property(c => c.EventType).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(32).IsRequired();
        builder.Property(c => c.TransactionId).HasMaxLength(128);
        builder.Property(c => c.PayloadJson).IsRequired();

        builder.HasIndex(c => c.OrderId).HasDatabaseName("IX_OrderPaymentCallbacks_OrderId");
        builder.HasIndex(c => c.OccurredOnUtc).HasDatabaseName("IX_OrderPaymentCallbacks_OccurredOnUtc");
    }
}
