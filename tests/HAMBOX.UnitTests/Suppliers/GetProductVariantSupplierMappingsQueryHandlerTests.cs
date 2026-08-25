using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// The product-centric mapping drawer's and the product edit page's Supplier Fulfillment card's shared
/// data source — proves the "resolve across every supplier, per variant" behavior that no existing
/// (supplier-scoped) query provides.
/// </summary>
public sealed class GetProductVariantSupplierMappingsQueryHandlerTests
{
    private static (Product Product, ProductVariant Variant) CreateVariant(
        HAMBOX.Modules.Catalog.Application.Abstractions.ICatalogDbContext catalogDb, Product product, string sku)
    {
        var variant = ProductVariant.Create(product.Id, sku);
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        return (product, variant);
    }

    [Fact]
    public async Task Handle_UnmappedVariant_ReportsUnmappedWithNoSupplierFields()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var product = Product.Create("أ", "Lonely Product", "d", "d", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var (_, variant) = CreateVariant(catalogDb, product, "SKU-1");
        await catalogDb.SaveChangesAsync();

        var handler = new GetProductVariantSupplierMappingsQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetProductVariantSupplierMappingsQuery(product.Id), CancellationToken.None);

        var dto = Assert.Single(result.Value);
        Assert.Equal(variant.Id, dto.VariantId);
        Assert.Equal("Unmapped", dto.MappingStatus);
        Assert.Null(dto.MappingId);
        Assert.Null(dto.SupplierId);
        Assert.Null(dto.SupplierName);
    }

    [Fact]
    public async Task Handle_VariantSpecificMapping_WinsOverProductWideMapping()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplierA = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        var supplierB = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.AddRange(supplierA, supplierB);

        var product = Product.Create("أ", "Product", "d", "d", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var (_, variant) = CreateVariant(catalogDb, product, "SKU-1");
        await catalogDb.SaveChangesAsync();

        var productWide = SupplierProductMapping.Create(supplierA.Id, product.Id, "PRODUCT-WIDE", null, null, 5m, "USD", 100);
        var variantSpecific = SupplierProductMapping.Create(supplierB.Id, product.Id, "VARIANT-SPECIFIC", null, null, 8m, "CAD", 100, variant.Id);
        db.SupplierProductMappings.AddRange(productWide, variantSpecific);
        await db.SaveChangesAsync();

        var handler = new GetProductVariantSupplierMappingsQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetProductVariantSupplierMappingsQuery(product.Id), CancellationToken.None);

        var dto = Assert.Single(result.Value);
        Assert.Equal("Mapped", dto.MappingStatus);
        Assert.Equal(variantSpecific.Id, dto.MappingId);
        Assert.Equal(supplierB.Id, dto.SupplierId);
        Assert.Equal("Bamboo", dto.SupplierName);
        Assert.Equal("VARIANT-SPECIFIC", dto.ExternalProductId);
    }

    [Fact]
    public async Task Handle_TwoVariants_MappedToDifferentSuppliers_ResolvesBothIndependently()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplierA = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        var supplierB = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.AddRange(supplierA, supplierB);

        var product = Product.Create("أ", "Two Variant Product", "d", "d", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var (_, variantGlobal) = CreateVariant(catalogDb, product, "SKU-GLOBAL");
        var (_, variantUs) = CreateVariant(catalogDb, product, "SKU-US");
        await catalogDb.SaveChangesAsync();

        db.SupplierProductMappings.Add(SupplierProductMapping.Create(
            supplierA.Id, product.Id, "EXT-A", null, null, 5m, "USD", 100, variantGlobal.Id));
        db.SupplierProductMappings.Add(SupplierProductMapping.Create(
            supplierB.Id, product.Id, "EXT-B", null, null, 6m, "CAD", 100, variantUs.Id));
        await db.SaveChangesAsync();

        var handler = new GetProductVariantSupplierMappingsQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetProductVariantSupplierMappingsQuery(product.Id), CancellationToken.None);

        Assert.Equal(2, result.Value.Count);
        var globalDto = result.Value.Single(d => d.VariantId == variantGlobal.Id);
        var usDto = result.Value.Single(d => d.VariantId == variantUs.Id);
        Assert.Equal("Acme", globalDto.SupplierName);
        Assert.Equal("Bamboo", usDto.SupplierName);
    }

    [Fact]
    public async Task Handle_ProductWithNoEligibleVariants_ReturnsEmpty()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var handler = new GetProductVariantSupplierMappingsQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetProductVariantSupplierMappingsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
