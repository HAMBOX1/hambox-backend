using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Proves the admin mappings list can show real product/variant names instead of raw GUIDs — the whole
/// point of the Phase 10 mapping-list redesign — without ever touching mapping persistence itself
/// (<c>InternalProductId</c>/<c>InternalProductVariantId</c> remain exactly what was stored).
/// </summary>
public sealed class GetSupplierMappingsQueryHandlerTests
{
    [Fact]
    public async Task Handle_EnrichesMappings_WithInternalProductNameAndVariantSku()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);

        var product = Product.Create("بطاقة أمازون", "Amazon Gift Card", "desc ar", "desc en", 10m, Guid.NewGuid());
        catalogDb.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, "AMAZON-US-10");
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var productWideMapping = SupplierProductMapping.Create(supplier.Id, product.Id, "439685", null, null, 10m, "USD", 100);
        var variantMapping = SupplierProductMapping.Create(supplier.Id, product.Id, "439686", null, null, 25m, "USD", 50, variant.Id);
        db.SupplierProductMappings.AddRange(productWideMapping, variantMapping);
        await db.SaveChangesAsync();

        var handler = new GetSupplierMappingsQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetSupplierMappingsQuery(supplier.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var productWideDto = result.Value.Single(m => m.Id == productWideMapping.Id);
        Assert.Equal("Amazon Gift Card", productWideDto.InternalProductName);
        Assert.Null(productWideDto.InternalVariantSku); // product-wide -> no variant to show

        var variantDto = result.Value.Single(m => m.Id == variantMapping.Id);
        Assert.Equal("Amazon Gift Card", variantDto.InternalProductName);
        Assert.Equal("AMAZON-US-10", variantDto.InternalVariantSku);
    }

    [Fact]
    public async Task Handle_ProductNoLongerFound_ReturnsNullName_RatherThanFailing()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create(); // deliberately empty — product was deleted after mapping was created

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, Guid.NewGuid(), "439685", null, null, 10m, "USD", 100);
        db.SupplierProductMappings.Add(mapping);
        await db.SaveChangesAsync();

        var handler = new GetSupplierMappingsQueryHandler(db, catalogDb);
        var result = await handler.Handle(new GetSupplierMappingsQuery(supplier.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value);
        Assert.Null(dto.InternalProductName);
        Assert.Equal(mapping.InternalProductId, dto.InternalProductId); // persisted id itself is untouched
    }

    [Fact]
    public async Task Handle_UnknownSupplier_StillFails_EnrichmentDoesNotChangeThatBehavior()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();
        var handler = new GetSupplierMappingsQueryHandler(db, catalogDb);

        var result = await handler.Handle(new GetSupplierMappingsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }
}
