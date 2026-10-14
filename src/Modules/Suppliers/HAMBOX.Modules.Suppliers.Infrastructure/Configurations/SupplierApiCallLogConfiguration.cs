using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Configurations;

internal sealed class SupplierApiCallLogConfiguration : IEntityTypeConfiguration<SupplierApiCallLog>
{
    public void Configure(EntityTypeBuilder<SupplierApiCallLog> builder)
    {
        builder.ToTable("SupplierApiCallLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ProviderKey).HasMaxLength(64).IsRequired();
        builder.Property(l => l.Method).HasMaxLength(10).IsRequired();
        builder.Property(l => l.Url).HasMaxLength(1000).IsRequired();
        builder.Property(l => l.RequestBody).HasMaxLength(SupplierApiCallLog.MaxBodyLength);
        builder.Property(l => l.ResponseBody).HasMaxLength(SupplierApiCallLog.MaxBodyLength);
        builder.Property(l => l.ResponseSummary).HasMaxLength(SupplierApiCallLog.MaxBodyLength);
        builder.Property(l => l.ErrorMessage).HasMaxLength(500);

        builder.HasIndex(l => l.CreatedOnUtc).HasDatabaseName("IX_SupplierApiCallLogs_CreatedOnUtc");
        builder.HasIndex(l => l.OrderId).HasDatabaseName("IX_SupplierApiCallLogs_OrderId");
        builder.HasIndex(l => l.FulfillmentId).HasDatabaseName("IX_SupplierApiCallLogs_FulfillmentId");
        builder.HasIndex(l => l.HamboxReferenceId).HasDatabaseName("IX_SupplierApiCallLogs_HamboxReferenceId");
        builder.HasIndex(l => new { l.SupplierId, l.CreatedOnUtc }).HasDatabaseName("IX_SupplierApiCallLogs_SupplierId_CreatedOnUtc");
    }
}
