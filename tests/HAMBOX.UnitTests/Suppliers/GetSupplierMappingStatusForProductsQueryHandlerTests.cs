using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Cross-supplier, business-friendly mapping status for the admin product list's Supplier Mapping column
/// and filter — pure DB read, no provider calls, so it's safe to run for the whole catalog when resolving
/// a filter's matching id-set.
/// </summary>
public sealed class GetSupplierMappingStatusForProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_NoMappings_ReportsUnmapped()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([]);

        var product = Product.Create("أ", "Lonely Product", "d", "d", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, "SKU-1");
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var handler = new GetSupplierMappingStatusForProductsQueryHandler(db, catalogDb, registry);
        var result = await handler.Handle(new GetSupplierMappingStatusForProductsQuery([product.Id]), CancellationToken.None);

        var status = result.Value[product.Id];
        Assert.Equal("Unmapped", status.Status);
        Assert.Equal(0, status.MappedVariantCount);
        Assert.Equal(1, status.TotalVariantCount);
    }

    [Fact]
    public async Task Handle_AllVariantsMapped_ByAReadySupplier_ReportsFullyMapped()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        supplier.UpdateCredentials("key", "secret", null, null, null, null);
        db.Suppliers.Add(supplier);

        var product = Product.Create("أ", "Fully Mapped Product", "d", "d", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, "SKU-1");
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        db.SupplierProductMappings.Add(SupplierProductMapping.Create(
            supplier.Id, product.Id, "EXT-1", null, null, 5m, "USD", 100, variant.Id));
        await db.SaveChangesAsync();

        var registry = new SupplierProviderRegistry([new TestDoubles.FakeSupplierProvider("Bamboo")]);
        var handler = new GetSupplierMappingStatusForProductsQueryHandler(db, catalogDb, registry);
        var result = await handler.Handle(new GetSupplierMappingStatusForProductsQuery([product.Id]), CancellationToken.None);

        var status = result.Value[product.Id];
        Assert.Equal("FullyMapped", status.Status);
        Assert.Equal(1, status.MappedVariantCount);
        Assert.Equal("Bamboo", status.PrimarySupplierName);
    }

    [Fact]
    public async Task Handle_MappedButProviderNotRegistered_ReportsMappingError()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier); // no credentials configured, and provider registry below has nothing registered

        var product = Product.Create("أ", "Broken Product", "d", "d", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, "SKU-1");
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        db.SupplierProductMappings.Add(SupplierProductMapping.Create(
            supplier.Id, product.Id, "EXT-1", null, null, 5m, "USD", 100, variant.Id));
        await db.SaveChangesAsync();

        var registry = new SupplierProviderRegistry([]); // nothing registered
        var handler = new GetSupplierMappingStatusForProductsQueryHandler(db, catalogDb, registry);
        var result = await handler.Handle(new GetSupplierMappingStatusForProductsQuery([product.Id]), CancellationToken.None);

        Assert.Equal("MappingError", result.Value[product.Id].Status);
    }

    [Fact]
    public async Task Handle_OneOfTwoVariantsMapped_ReportsPartiallyMapped()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        supplier.UpdateCredentials("key", "secret", null, null, null, null);
        db.Suppliers.Add(supplier);

        var product = Product.Create("أ", "Two Variant Product", "d", "d", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var mappedVariant = ProductVariant.Create(product.Id, "SKU-MAPPED");
        mappedVariant.Activate();
        var unmappedVariant = ProductVariant.Create(product.Id, "SKU-UNMAPPED");
        unmappedVariant.Activate();
        catalogDb.ProductVariants.AddRange(mappedVariant, unmappedVariant);
        await catalogDb.SaveChangesAsync();

        db.SupplierProductMappings.Add(SupplierProductMapping.Create(
            supplier.Id, product.Id, "EXT-1", null, null, 5m, "USD", 100, mappedVariant.Id));
        await db.SaveChangesAsync();

        var registry = new SupplierProviderRegistry([new TestDoubles.FakeSupplierProvider("Bamboo")]);
        var handler = new GetSupplierMappingStatusForProductsQueryHandler(db, catalogDb, registry);
        var result = await handler.Handle(new GetSupplierMappingStatusForProductsQuery([product.Id]), CancellationToken.None);

        var status = result.Value[product.Id];
        Assert.Equal("PartiallyMapped", status.Status);
        Assert.Equal(1, status.MappedVariantCount);
        Assert.Equal(2, status.TotalVariantCount);
    }
}
