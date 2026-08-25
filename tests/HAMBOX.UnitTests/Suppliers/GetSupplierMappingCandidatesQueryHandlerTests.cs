using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// The Map Products workspace's core data source — every eligible (active, visible) HAMBOX product
/// variant, next to whatever mapping this specific supplier already has for it, if any.
/// </summary>
public sealed class GetSupplierMappingCandidatesQueryHandlerTests
{
    [Fact]
    public async Task Handle_UnmappedVariant_HasNullExistingMapping()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var product = Product.Create("لعبة", "Game Pass", "desc ar", "desc en", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, "GAMEPASS-GLOBAL");
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var handler = new GetSupplierMappingCandidatesQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetSupplierMappingCandidatesQuery(supplier.Id, null, "all", 1, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var candidate = Assert.Single(result.Value.Items);
        Assert.Equal(product.Id, candidate.ProductId);
        Assert.Equal(variant.Id, candidate.VariantId);
        Assert.Null(candidate.ExistingMappingId);
        Assert.Equal("GAMEPASS-GLOBAL", candidate.VariantDisplayName); // no options -> falls back to SKU
    }

    [Fact]
    public async Task Handle_VariantSpecificMapping_WinsOverProductWideMapping()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);

        var product = Product.Create("لعبة", "Game Pass", "desc ar", "desc en", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, "GAMEPASS-CA");
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var productWide = SupplierProductMapping.Create(supplier.Id, product.Id, "PRODUCT-WIDE", null, null, 5m, "USD", 100);
        var variantSpecific = SupplierProductMapping.Create(supplier.Id, product.Id, "VARIANT-SPECIFIC", null, null, 8m, "CAD", 100, variant.Id);
        db.SupplierProductMappings.AddRange(productWide, variantSpecific);
        await db.SaveChangesAsync();

        var handler = new GetSupplierMappingCandidatesQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetSupplierMappingCandidatesQuery(supplier.Id, null, "all", 1, 20), CancellationToken.None);

        var candidate = Assert.Single(result.Value.Items);
        Assert.Equal(variantSpecific.Id, candidate.ExistingMappingId);
        Assert.Equal("VARIANT-SPECIFIC", candidate.ExternalProductId);
    }

    [Fact]
    public async Task Handle_StatusFilter_NarrowsToMappedOrUnmapped()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);

        var mappedProduct = Product.Create("أ", "Mapped Product", "d", "d", 10m, Guid.NewGuid());
        mappedProduct.Activate();
        var unmappedProduct = Product.Create("ب", "Unmapped Product", "d", "d", 10m, Guid.NewGuid());
        unmappedProduct.Activate();
        catalogDb.Products.AddRange(mappedProduct, unmappedProduct);

        var mappedVariant = ProductVariant.Create(mappedProduct.Id, "MAPPED-SKU");
        mappedVariant.Activate();
        var unmappedVariant = ProductVariant.Create(unmappedProduct.Id, "UNMAPPED-SKU");
        unmappedVariant.Activate();
        catalogDb.ProductVariants.AddRange(mappedVariant, unmappedVariant);
        await catalogDb.SaveChangesAsync();

        db.SupplierProductMappings.Add(SupplierProductMapping.Create(
            supplier.Id, mappedProduct.Id, "EXT-1", null, null, 5m, "USD", 100, mappedVariant.Id));
        await db.SaveChangesAsync();

        var handler = new GetSupplierMappingCandidatesQueryHandler(db, catalogDb);

        var mappedOnly = await handler.Handle(new GetSupplierMappingCandidatesQuery(supplier.Id, null, "mapped", 1, 20), CancellationToken.None);
        Assert.Equal(mappedProduct.Id, Assert.Single(mappedOnly.Value.Items).ProductId);

        var unmappedOnly = await handler.Handle(new GetSupplierMappingCandidatesQuery(supplier.Id, null, "unmapped", 1, 20), CancellationToken.None);
        Assert.Equal(unmappedProduct.Id, Assert.Single(unmappedOnly.Value.Items).ProductId);
    }

    [Fact]
    public async Task Handle_InactiveOrHiddenVariant_IsExcluded()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var draftProduct = Product.Create("مسودة", "Draft Product", "d", "d", 10m, Guid.NewGuid());
        // Deliberately never activated — Status stays Draft, not eligible for mapping.
        catalogDb.Products.Add(draftProduct);
        var variant = ProductVariant.Create(draftProduct.Id, "DRAFT-SKU");
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var handler = new GetSupplierMappingCandidatesQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetSupplierMappingCandidatesQuery(supplier.Id, null, "all", 1, 20), CancellationToken.None);

        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task Handle_UnknownSupplier_Fails()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();
        var handler = new GetSupplierMappingCandidatesQueryHandler(db, catalogDb);

        var result = await handler.Handle(new GetSupplierMappingCandidatesQuery(Guid.NewGuid(), null, "all", 1, 20), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }
}
