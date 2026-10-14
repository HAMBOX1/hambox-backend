using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class PaymentWebhookLogConfiguration : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.ToTable("PaymentWebhookLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Provider).HasMaxLength(64).IsRequired();
        builder.Property(l => l.SignatureVerificationResult).HasMaxLength(32).IsRequired();
        builder.Property(l => l.ProcessingResult).HasMaxLength(64).IsRequired();
        builder.Property(l => l.GatewayReference).HasMaxLength(200);
        builder.Property(l => l.MerchantReference).HasMaxLength(200);
        builder.Property(l => l.RawPayload).HasMaxLength(PaymentWebhookLog.MaxRawPayloadLength).IsRequired();
        builder.Property(l => l.SourceIp).HasMaxLength(64);

        builder.HasIndex(l => l.ReceivedOnUtc).HasDatabaseName("IX_PaymentWebhookLogs_ReceivedOnUtc");
        builder.HasIndex(l => l.OrderId).HasDatabaseName("IX_PaymentWebhookLogs_OrderId");
        builder.HasIndex(l => new { l.Provider, l.ProcessingResult }).HasDatabaseName("IX_PaymentWebhookLogs_Provider_Result");
    }
}
