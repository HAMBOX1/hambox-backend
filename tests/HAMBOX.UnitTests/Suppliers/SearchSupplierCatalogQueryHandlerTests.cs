using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Proves the catalog-search endpoint resolves the supplier's provider exactly like every other
/// supplier-scoped operation (registry lookup + <see cref="SupplierProviderContext"/>), so credentials
/// only ever flow server-side, and that a provider with no catalog reports that honestly rather than an
/// empty success.
/// </summary>
public sealed class SearchSupplierCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_UnknownSupplier_Fails()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Bamboo")]);
        var handler = new SearchSupplierCatalogQueryHandler(db, registry);

        var result = await handler.Handle(new SearchSupplierCatalogQuery(Guid.NewGuid(), null, 1, 20), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Handle_ProviderTypeNotRegistered_Fails()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var registry = new SupplierProviderRegistry([]); // nothing registered
        var handler = new SearchSupplierCatalogQueryHandler(db, registry);

        var result = await handler.Handle(new SearchSupplierCatalogQuery(supplier.Id, null, 1, 20), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_ManualProvider_ReportsNoCatalog_NotAnEmptySuccess()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Acme", "ACME", "Manual", SupplierAuthenticationType.None, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var registry = new SupplierProviderRegistry([new ManualSupplierProvider()]);
        var handler = new SearchSupplierCatalogQueryHandler(db, registry);

        var result = await handler.Handle(new SearchSupplierCatalogQuery(supplier.Id, null, 1, 20), CancellationToken.None);

        Assert.True(result.IsSuccess); // the query itself succeeded — the provider's answer is inside the DTO
        Assert.False(result.Value.IsSuccess); // but the catalog search itself reports unsupported, not an empty result
        Assert.Empty(result.Value.Items);
        Assert.NotNull(result.Value.Message);
    }

    [Fact]
    public async Task Handle_MapsProviderItems_ToSafeDtoFields_Only()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var fake = new FakeSupplierProvider("Bamboo")
        {
            CatalogResponse = new SupplierCatalogSearchResult(
                true,
                [new SupplierCatalogItem("439685", "Amazon Gift Card $10", "Amazon", "USD", 10m, 10m, true)],
                null),
        };
        var registry = new SupplierProviderRegistry([fake]);
        var handler = new SearchSupplierCatalogQueryHandler(db, registry);

        var result = await handler.Handle(new SearchSupplierCatalogQuery(supplier.Id, "amazon", 1, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("439685", item.ExternalProductId);
        Assert.Equal("Amazon Gift Card $10", item.Name);
        Assert.Equal("Amazon", item.BrandName);
        Assert.Equal("USD", item.Currency);
        Assert.True(item.Available);
    }
}
