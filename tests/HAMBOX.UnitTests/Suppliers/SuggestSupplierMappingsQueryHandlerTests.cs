using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Suppliers.TestDoubles;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Live, on-demand auto-matching — never a cached catalog (Bamboo has no bulk export). Proves the
/// confidence-tier thresholds and that a low/no-confidence result never carries a <c>BestMatch</c> a
/// caller could mistake for something safe to auto-apply.
/// </summary>
public sealed class SuggestSupplierMappingsQueryHandlerTests
{
    private static (Product Product, ProductVariant Variant) CreateProduct(HAMBOX.Modules.Catalog.Application.Abstractions.ICatalogDbContext catalogDb, string nameEn, string sku)
    {
        var product = Product.Create("منتج", nameEn, "desc ar", "desc en", 10m, Guid.NewGuid());
        product.Activate();
        catalogDb.Products.Add(product);
        var variant = ProductVariant.Create(product.Id, sku);
        variant.Activate();
        catalogDb.ProductVariants.Add(variant);
        return (product, variant);
    }

    [Fact]
    public async Task Handle_ExactNameMatch_ScoresHighConfidence()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var (product, variant) = CreateProduct(catalogDb, "Xbox Game Pass Ultimate 3 Months", "GAMEPASS-3M");
        await catalogDb.SaveChangesAsync();

        var fake = new FakeSupplierProvider("Bamboo")
        {
            CatalogResponse = new SupplierCatalogSearchResult(
                true,
                [new SupplierCatalogItem("1294161", "Xbox Game Pass Ultimate 3 Months", "Xbox", "USD", 45m, 45m, true)],
                null),
        };
        var registry = new SupplierProviderRegistry([fake]);
        var handler = new SuggestSupplierMappingsQueryHandler(db, catalogDb, registry);

        var result = await handler.Handle(
            new SuggestSupplierMappingsQuery(supplier.Id, [new SuggestionCandidate(product.Id, variant.Id)]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var suggestion = Assert.Single(result.Value);
        Assert.Equal("High", suggestion.ConfidenceTier);
        Assert.NotNull(suggestion.BestMatch);
        Assert.Equal("1294161", suggestion.BestMatch!.ExternalProductId);
        Assert.True(suggestion.ConfidenceScore >= 80);
    }

    [Fact]
    public async Task Handle_UnrelatedCatalogResults_ScoreNoMatch_AndCarryNoBestMatch()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var (product, variant) = CreateProduct(catalogDb, "Xbox Game Pass Ultimate 3 Months", "GAMEPASS-3M");
        await catalogDb.SaveChangesAsync();

        var fake = new FakeSupplierProvider("Bamboo")
        {
            CatalogResponse = new SupplierCatalogSearchResult(
                true,
                [new SupplierCatalogItem("999", "Completely Unrelated Gift Card", "Acme", "USD", 5m, 5m, true)],
                null),
        };
        var registry = new SupplierProviderRegistry([fake]);
        var handler = new SuggestSupplierMappingsQueryHandler(db, catalogDb, registry);

        var result = await handler.Handle(
            new SuggestSupplierMappingsQuery(supplier.Id, [new SuggestionCandidate(product.Id, variant.Id)]), CancellationToken.None);

        var suggestion = Assert.Single(result.Value);
        Assert.Equal("None", suggestion.ConfidenceTier);
        Assert.Null(suggestion.BestMatch); // never a match a caller could mistake for confirmable
    }

    [Fact]
    public async Task Handle_ProviderSearchFails_NeverThrows_ReportsNoMatchForThatCandidate()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var (product, variant) = CreateProduct(catalogDb, "Xbox Game Pass Ultimate 3 Months", "GAMEPASS-3M");
        await catalogDb.SaveChangesAsync();

        var fake = new FakeSupplierProvider("Bamboo")
        {
            CatalogResponse = new SupplierCatalogSearchResult(false, [], "provider unreachable"),
        };
        var registry = new SupplierProviderRegistry([fake]);
        var handler = new SuggestSupplierMappingsQueryHandler(db, catalogDb, registry);

        var result = await handler.Handle(
            new SuggestSupplierMappingsQuery(supplier.Id, [new SuggestionCandidate(product.Id, variant.Id)]), CancellationToken.None);

        Assert.True(result.IsSuccess); // a per-candidate provider failure never fails the whole batch
        var suggestion = Assert.Single(result.Value);
        Assert.Equal("None", suggestion.ConfidenceTier);
    }

    [Fact]
    public async Task Handle_NoCandidates_ReturnsEmpty_WithoutCallingProvider()
    {
        await using var db = SuppliersTestDbContextFactory.Create();
        var catalogDb = TestCatalogDbContextFactory.Create();

        var supplier = Supplier.Create("Bamboo", "BAMBOO", "Bamboo", SupplierAuthenticationType.BasicAuth, null, 100);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Bamboo")]);
        var handler = new SuggestSupplierMappingsQueryHandler(db, catalogDb, registry);

        var result = await handler.Handle(new SuggestSupplierMappingsQuery(supplier.Id, []), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
