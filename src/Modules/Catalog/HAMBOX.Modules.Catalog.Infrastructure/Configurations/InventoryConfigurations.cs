using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

internal sealed class ProductPlanConfiguration : IEntityTypeConfiguration<ProductPlan>
{
    public void Configure(EntityTypeBuilder<ProductPlan> builder)
    {
        builder.ToTable("ProductPlans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.ProductId, x.Slug }).IsUnique();
    }
}

internal sealed class ProductOptionGroupConfiguration : IEntityTypeConfiguration<ProductOptionGroup>
{
    public void Configure(EntityTypeBuilder<ProductOptionGroup> builder)
    {
        builder.ToTable("ProductOptionGroups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.ProductId, x.Key }).IsUnique().HasFilter("[ParentOptionId] IS NULL");
        builder.HasIndex(x => new { x.ParentOptionId, x.Key }).IsUnique().HasFilter("[ParentOptionId] IS NOT NULL");
        builder.HasOne<ProductOption>().WithMany().HasForeignKey(x => x.ParentOptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.OptionGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Options).HasField("_options");
    }
}

internal sealed class ProductOptionConfiguration : IEntityTypeConfiguration<ProductOption>
{
    public void Configure(EntityTypeBuilder<ProductOption> builder)
    {
        builder.ToTable("ProductOptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DescriptionHtml).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.OptionGroupId, x.Value }).IsUnique();
    }
}

internal sealed class OptionGroupTemplateConfiguration : IEntityTypeConfiguration<OptionGroupTemplate>
{
    public void Configure(EntityTypeBuilder<OptionGroupTemplate> builder)
    {
        builder.ToTable("OptionGroupTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Options).HasField("_options");
    }
}

internal sealed class OptionGroupTemplateOptionConfiguration : IEntityTypeConfiguration<OptionGroupTemplateOption>
{
    public void Configure(EntityTypeBuilder<OptionGroupTemplateOption> builder)
    {
        builder.ToTable("OptionGroupTemplateOptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DescriptionHtml).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.TemplateId, x.Value }).IsUnique();
    }
}

internal sealed class OptionDescriptionTemplateConfiguration : IEntityTypeConfiguration<OptionDescriptionTemplate>
{
    public void Configure(EntityTypeBuilder<OptionDescriptionTemplate> builder)
    {
        builder.ToTable("OptionDescriptionTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DescriptionHtml).IsRequired().HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PriceOverride).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ComparePrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        // Explicit DB-level default so every existing row gets ManualOnly (never an empty string that
        // Enum.Parse would throw on) — the safe, non-spending default this column must never be blank for.
        builder.Property(x => x.FulfillmentMode).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(FulfillmentMode.ManualOnly);
        // Filtered so a permanently-deleted (soft-deleted) variant's SKU frees up for reuse —
        // SoftDelete() deliberately leaves Sku untouched (see its doc comment), so without this
        // filter a regenerated variant with the same option combination collides with the
        // tombstoned row's SKU and GenerateProductVariantsCommandHandler's whole batch insert
        // fails. Same pattern as Legal's Slug unique index.
        builder.HasIndex(x => x.Sku).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.ProductId);
        builder.HasMany(x => x.SelectedOptions).WithOne().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.SelectedOptions).HasField("_selectedOptions");
    }
}

internal sealed class ProductVariantOptionConfiguration : IEntityTypeConfiguration<ProductVariantOption>
{
    public void Configure(EntityTypeBuilder<ProductVariantOption> builder)
    {
        builder.ToTable("ProductVariantOptions");
        builder.HasKey(x => new { x.VariantId, x.OptionId });
    }
}

internal sealed class InventorySupplierConfiguration : IEntityTypeConfiguration<InventorySupplier>
{
    public void Configure(EntityTypeBuilder<InventorySupplier> builder)
    {
        builder.ToTable("InventorySuppliers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.CompanyName);
    }
}

internal sealed class InventoryBatchConfiguration : IEntityTypeConfiguration<InventoryBatch>
{
    public void Configure(EntityTypeBuilder<InventoryBatch> builder)
    {
        builder.ToTable("InventoryBatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.PurchaseCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ExpectedMargin).HasColumnType("decimal(18,2)");
        builder.HasIndex(x => x.VariantId);
        builder.HasIndex(x => x.SupplierId);
    }
}

internal sealed class DigitalInventoryCodeConfiguration : IEntityTypeConfiguration<DigitalInventoryCode>
{
    public void Configure(EntityTypeBuilder<DigitalInventoryCode> builder)
    {
        builder.ToTable("DigitalInventoryCodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DigitalCode).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CodeHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SerialNumber).HasMaxLength(500);
        builder.Property(x => x.Pin).HasMaxLength(300);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.PurchaseCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.SellingPriceOverride).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.CodeHash).IsUnique();
        builder.HasIndex(x => new { x.VariantId, x.Status });
        builder.HasIndex(x => x.BatchId);
    }
}

internal sealed class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        builder.ToTable("InventoryReservations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CodeId);
        builder.HasIndex(x => new { x.IsActive, x.ExpiresOnUtc });
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CartId);
    }
}

internal sealed class InventoryAuditLogConfiguration : IEntityTypeConfiguration<InventoryAuditLog>
{
    public void Configure(EntityTypeBuilder<InventoryAuditLog> builder)
    {
        builder.ToTable("InventoryAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.HasIndex(x => x.OccurredOnUtc);
        builder.HasIndex(x => x.VariantId);
        builder.HasIndex(x => x.OrderId);
    }
}
