using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Configurations;

internal sealed class SupplierFulfillmentConfiguration : IEntityTypeConfiguration<SupplierFulfillment>
{
    public void Configure(EntityTypeBuilder<SupplierFulfillment> builder)
    {
        builder.ToTable("SupplierFulfillments");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.ProviderOrderId).HasMaxLength(200);
        builder.Property(f => f.ProviderAccountId).HasMaxLength(200);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.FailureCategory).HasConversion<string>().HasMaxLength(40);
        builder.Property(f => f.FailureDetail).HasMaxLength(500);
        builder.Property(f => f.CreatedBy).HasMaxLength(128);
        builder.Property(f => f.ModifiedBy).HasMaxLength(128);
        builder.Property(f => f.RowVersion).IsRowVersion();

        // The idempotency barrier: this HamboxReferenceId (the provider RequestId) is generated once
        // and reused forever for a given attempt, so it can never collide with another attempt's.
        builder.HasIndex(f => f.HamboxReferenceId).IsUnique().HasDatabaseName("IX_SupplierFulfillments_HamboxReferenceId");

        // Once the provider confirms a transaction/order id, no two attempts against the same supplier
        // may claim the same provider-side reference. Filtered because ProviderOrderId starts null.
        // Unquoted column name in the filter (not "[ProviderOrderId]"): both SQL Server and SQLite
        // accept a bare, non-reserved identifier here, so the same configuration works whether the
        // model builds a real migration (SQL Server) or a test's EnsureCreated() schema (SQLite).
        builder.HasIndex(f => new { f.SupplierId, f.ProviderOrderId })
            .IsUnique()
            .HasDatabaseName("IX_SupplierFulfillments_SupplierId_ProviderOrderId")
            .HasFilter("ProviderOrderId IS NOT NULL");

        // Reconciliation sweeps scan by status; lookups from Commerce scan by order/order item.
        builder.HasIndex(f => f.Status).HasDatabaseName("IX_SupplierFulfillments_Status");
        builder.HasIndex(f => new { f.OrderId, f.OrderItemId }).HasDatabaseName("IX_SupplierFulfillments_OrderId_OrderItemId");

        builder.Ignore(f => f.DomainEvents);
        builder.Ignore(f => f.RemainingQuantity);
        builder.Ignore(f => f.IsTerminal);
    }
}
