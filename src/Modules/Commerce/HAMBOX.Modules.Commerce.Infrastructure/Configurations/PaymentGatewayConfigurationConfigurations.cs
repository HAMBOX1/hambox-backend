using HAMBOX.Modules.Commerce.Domain.PaymentGateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class PaymentGatewayConfigurationConfiguration : IEntityTypeConfiguration<PaymentGatewayConfiguration>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayConfiguration> builder)
    {
        builder.ToTable("PaymentGatewayConfigurations");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.GatewayKey).HasMaxLength(50).IsRequired();
        builder.Property(g => g.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(g => g.FeePercent).HasPrecision(5, 2);
        builder.Property(g => g.BaseUrl).HasMaxLength(500);
        builder.Property(g => g.AccountId).HasMaxLength(200);
        builder.Property(g => g.SecondaryId).HasMaxLength(200);
        builder.Property(g => g.WebhookUrl).HasMaxLength(500);
        builder.Property(g => g.FrontendResultUrl).HasMaxLength(500);
        // No explicit HasColumnType: EF Core's SQL Server convention already maps an unbounded string
        // to nvarchar(max) — mirrors SupplierConfiguration.SettingsJson.
        builder.Property(g => g.AdditionalConfigJson);

        builder.HasIndex(g => g.GatewayKey).IsUnique();
    }
}
