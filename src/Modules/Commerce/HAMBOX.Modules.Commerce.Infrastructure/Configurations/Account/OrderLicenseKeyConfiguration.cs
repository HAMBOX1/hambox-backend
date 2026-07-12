using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="OrderLicenseKey"/> entity for Entity Framework Core.
/// </summary>
internal sealed class OrderLicenseKeyConfiguration : IEntityTypeConfiguration<OrderLicenseKey>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrderLicenseKey> builder)
    {
        builder.ToTable("OrderLicenseKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.OrderId)
            .IsRequired();

        builder.Property(k => k.OrderItemId)
            .IsRequired();

        builder.Property(k => k.ProductId)
            .IsRequired();

        builder.Property(k => k.ProductVariantId);
        builder.Property(k => k.DigitalInventoryCodeId);

        builder.Property(k => k.LicenseKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(k => k.CreatedOnUtc)
            .IsRequired();

        builder.Property(k => k.ModifiedOnUtc);

        builder.HasIndex(k => k.OrderId)
            .HasDatabaseName("IX_OrderLicenseKeys_OrderId");

        builder.HasIndex(k => k.OrderItemId)
            .HasDatabaseName("IX_OrderLicenseKeys_OrderItemId");
    }
}
