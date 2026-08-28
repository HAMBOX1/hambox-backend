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

        // Closes a real race in RequestFulfillmentAsync's "reuse the open attempt for this scope, else
        // create one" read-then-write: two callers (e.g. two routing-engine invocations racing on the
        // same order item, possibly on different API instances) could otherwise both observe "none
        // exists yet" and both insert a row for the same (order, item, supplier, mapping) — a real
        // double-purchase risk the RowVersion claim step alone does not prevent, since it only protects
        // a row that already exists. Filtered (not unique-forever) because a scope legitimately gets a
        // new row once the previous attempt reaches a terminal status (e.g. a PartialFailed follow-up).
        // SQL Server filtered-index predicates support only simple comparisons/AND (no IN/NOT IN) —
        // confirmed the hard way: "WHERE Status NOT IN (...)" is a real SQL Server syntax error
        // ("Incorrect syntax near 'NOT'"), caught by actually running this migration rather than assumed.
        builder.HasIndex(f => new { f.OrderId, f.OrderItemId, f.SupplierId, f.SupplierProductMappingId })
            .IsUnique()
            .HasDatabaseName("IX_SupplierFulfillments_Scope_NonTerminal")
            .HasFilter("Status <> 'Succeeded' AND Status <> 'PartialFailed' AND Status <> 'Failed'");

        builder.Ignore(f => f.DomainEvents);
        builder.Ignore(f => f.RemainingQuantity);
        builder.Ignore(f => f.IsTerminal);
    }
}

internal sealed class SupplierRoutingAuditLogConfiguration : IEntityTypeConfiguration<SupplierRoutingAuditLog>
{
    public void Configure(EntityTypeBuilder<SupplierRoutingAuditLog> builder)
    {
        builder.ToTable("SupplierRoutingAuditLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.SelectedCostInBaseCurrency).HasColumnType("decimal(18,2)");
        builder.Property(l => l.BaseCurrency).HasMaxLength(3).IsRequired();
        // No explicit HasColumnType — mirrors SupplierConfiguration.SettingsJson's identical rationale
        // (an unbounded JSON summary; EF's own convention already maps this to nvarchar(max) on SQL Server).
        builder.Property(l => l.CandidatesJson).IsRequired();
        builder.Property(l => l.CreatedBy).HasMaxLength(128);
        builder.Property(l => l.ModifiedBy).HasMaxLength(128);

        // Admin order-detail page reads by order; nothing else queries this table by any other shape.
        builder.HasIndex(l => new { l.OrderId, l.CreatedOnUtc }).HasDatabaseName("IX_SupplierRoutingAuditLogs_OrderId_CreatedOnUtc");

        builder.Ignore(l => l.DomainEvents);
    }
}
