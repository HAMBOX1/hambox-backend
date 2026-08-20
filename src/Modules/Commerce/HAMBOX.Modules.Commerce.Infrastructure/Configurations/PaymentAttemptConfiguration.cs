using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Provider).HasMaxLength(32).IsRequired();
        builder.Property(p => p.PartnerTxId).HasMaxLength(128).IsRequired();
        builder.Property(p => p.ProviderTransactionId).HasMaxLength(128);
        builder.Property(p => p.ExpectedAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.ExpectedCurrency).HasMaxLength(8).IsRequired();
        builder.Property(p => p.VerifiedAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.VerifiedCurrency).HasMaxLength(8);
        builder.Property(p => p.OperatorId).HasMaxLength(32).IsRequired();
        builder.Property(p => p.ServiceId).HasMaxLength(32).IsRequired();
        builder.Property(p => p.MaskedMsisdn).HasMaxLength(32);
        builder.Property(p => p.ProviderReferenceCode).HasMaxLength(64);
        builder.Property(p => p.LastReasonCode).HasMaxLength(16);
        builder.Property(p => p.LastReasonDescription).HasMaxLength(512);
        builder.Property(p => p.PendingPromotionsJson);
        builder.Property(p => p.RowVersion).IsRowVersion();

        // Guarantees the database itself — not just application logic — refuses to ever create a
        // second attempt under the same partner_txid for the same provider, and refuses to let two
        // different attempts settle against the same provider transaction id once known.
        builder.HasIndex(p => new { p.Provider, p.PartnerTxId })
            .IsUnique()
            .HasDatabaseName("IX_PaymentAttempts_Provider_PartnerTxId");

        builder.HasIndex(p => new { p.Provider, p.ProviderTransactionId })
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL")
            .HasDatabaseName("IX_PaymentAttempts_Provider_ProviderTransactionId");

        builder.HasIndex(p => p.OrderId).HasDatabaseName("IX_PaymentAttempts_OrderId");
        builder.HasIndex(p => new { p.Status, p.ExpiresOnUtc }).HasDatabaseName("IX_PaymentAttempts_Status_ExpiresOnUtc");
    }
}
