using System.Text;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;
using HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;
using HAMBOX.Modules.Catalog.Application.Features.Products.ExportProducts;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog;

/// <summary>
/// Audit R14: bulk operations over a selection of products (publish / unpublish / archive / change category /
/// adjust price) — per-product results, error aggregation, selection rules — and the CSV export of that selection.
/// </summary>
public sealed class BulkProductOperationsTests
{
    /// <summary>Routes the per-product commands the bulk handler fans out to the real handlers on the same context.</summary>
    private sealed class RoutingSender(TestCatalogDbContext db) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object result = request switch
            {
                PublishProductCommand c => await new PublishProductCommandHandler(db).Handle(c, cancellationToken),
                DeactivateProductCommand c => await new DeactivateProductCommandHandler(db).Handle(c, cancellationToken),
                ArchiveProductCommand c => await new ArchiveProductCommandHandler(db).Handle(c, cancellationToken),
                ChangeProductCategoryCommand c => await new ChangeProductCategoryCommandHandler(db).Handle(c, cancellationToken),
                AdjustProductPriceCommand c => await new AdjustProductPriceCommandHandler(db).Handle(c, cancellationToken),
                _ => throw new NotSupportedException(request.GetType().Name),
            };

            return (TResponse)result;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static async Task<(TestCatalogDbContext Db, Category CategoryA, Category CategoryB, List<Product> Products)> SeedAsync(
        int count = 3, decimal price = 100m)
    {
        var db = TestCatalogDbContextFactory.Create();
        var a = Category.Create("أ", "Category A", $"cat-a-{Guid.NewGuid():N}");
        var b = Category.Create("ب", "Category B", $"cat-b-{Guid.NewGuid():N}");
        db.Categories.AddRange(a, b);
        var products = Enumerable.Range(1, count)
            .Select(i => Product.Create($"منتج {i}", $"Product {i}", "d", "d", price, a.Id))
            .ToList();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return (db, a, b, products);
    }

    private static BulkProductsCommandHandler Handler(TestCatalogDbContext db) => new(db, new RoutingSender(db));

    private static BulkProductsCommand Command(
        IReadOnlyList<Guid>? ids,
        string action,
        Guid? targetCategory = null,
        PriceAdjustmentMode? mode = null,
        decimal? value = null,
        bool selectAll = false,
        ProductStatus? status = null) =>
        new(new BulkProductsRequest(ids, null, status, null, selectAll, action, targetCategory, mode, value));

    [Fact]
    public async Task Publish_ActivatesEverySelectedProduct()
    {
        var (db, _, _, products) = await SeedAsync();

        var result = await Handler(db).Handle(
            Command(products.Select(p => p.Id).ToList(), "publish"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.SuccessCount);
        Assert.Equal(0, result.Value.ErrorCount);
        Assert.All(await db.Products.ToListAsync(), p => Assert.Equal(ProductStatus.Active, p.Status));
    }

    [Fact]
    public async Task OneBadProduct_DoesNotStopTheRest_AndIsReportedByName()
    {
        var (db, _, _, products) = await SeedAsync();
        products[0].Activate();
        await db.SaveChangesAsync();

        // Publishing an already-active product is refused; the other two still go through.
        var result = await Handler(db).Handle(
            Command(products.Select(p => p.Id).ToList(), "publish"), CancellationToken.None);

        Assert.Equal(2, result.Value.SuccessCount);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Contains(products[0].Id.ToString(), Assert.Single(result.Value.Errors));
    }

    [Fact]
    public async Task Archive_ThenUnpublishIsRefused_ForTheArchivedOnes()
    {
        var (db, _, _, products) = await SeedAsync(2);
        var ids = products.Select(p => p.Id).ToList();

        var archived = await Handler(db).Handle(Command(ids, "archive"), CancellationToken.None);
        Assert.Equal(2, archived.Value.SuccessCount);

        var unpublish = await Handler(db).Handle(Command(ids, "unpublish"), CancellationToken.None);
        Assert.Equal(0, unpublish.Value.SuccessCount);
        Assert.Equal(2, unpublish.Value.ErrorCount);
    }

    [Fact]
    public async Task ChangeCategory_MovesEveryProduct()
    {
        var (db, _, categoryB, products) = await SeedAsync();

        var result = await Handler(db).Handle(
            Command(products.Select(p => p.Id).ToList(), "change-category", targetCategory: categoryB.Id), CancellationToken.None);

        Assert.Equal(3, result.Value.SuccessCount);
        Assert.All(await db.Products.ToListAsync(), p => Assert.Equal(categoryB.Id, p.CategoryId));
    }

    [Theory]
    [InlineData(PriceAdjustmentMode.IncreasePercent, 10, 110)]
    [InlineData(PriceAdjustmentMode.DecreasePercent, 20, 80)]
    [InlineData(PriceAdjustmentMode.SetFixed, 49.99, 49.99)]
    public async Task AdjustPrice_AppliesTheRuleToTheWholeSelection(PriceAdjustmentMode mode, double value, double expected)
    {
        var (db, _, _, products) = await SeedAsync();

        var result = await Handler(db).Handle(
            Command(products.Select(p => p.Id).ToList(), "adjust-price", mode: mode, value: (decimal)value), CancellationToken.None);

        Assert.Equal(3, result.Value.SuccessCount);
        Assert.All(await db.Products.ToListAsync(), p => Assert.Equal((decimal)expected, p.Price));
    }

    [Fact]
    public async Task SelectAllMatching_ResolvesTheFilterServerSide()
    {
        var (db, _, _, products) = await SeedAsync(4);
        products[0].Activate();
        products[1].Activate();
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(
            Command(null, "unpublish", selectAll: true, status: ProductStatus.Active), CancellationToken.None);

        Assert.Equal(2, result.Value.SuccessCount);
        Assert.Equal(2, await db.Products.CountAsync(p => p.Status == ProductStatus.Inactive));
        Assert.Equal(2, await db.Products.CountAsync(p => p.Status == ProductStatus.Draft));
    }

    [Fact]
    public async Task EmptySelection_AndMissingParameters_AreRefused()
    {
        var (db, _, _, products) = await SeedAsync(1);
        var handler = Handler(db);

        var noSelection = await handler.Handle(Command(null, "publish"), CancellationToken.None);
        Assert.True(noSelection.IsFailure);

        var ids = products.Select(p => p.Id).ToList();
        Assert.True((await handler.Handle(Command(ids, "change-category"), CancellationToken.None)).IsFailure);
        Assert.True((await handler.Handle(Command(ids, "adjust-price"), CancellationToken.None)).IsFailure);

        // An action the system does not know is reported per product (nothing changed), never applied.
        var unknown = await handler.Handle(Command(ids, "teleport"), CancellationToken.None);
        Assert.Equal(0, unknown.Value.SuccessCount);
        Assert.Equal(ids.Count, unknown.Value.ErrorCount);
    }

    // ─── CSV export of a selection ──────────────────────────────────────

    [Fact]
    public async Task Export_WritesOneRowPerSelectedProduct_WithEscapingAndStockCounts()
    {
        var (db, categoryA, _, _) = await SeedAsync(0);
        var tricky = Product.Create("منتج", "Steam, \"Gift\" 20", "d", "d", 20m, categoryA.Id);
        var plain = Product.Create("منتج", "Plain", "d", "d", 5m, categoryA.Id);
        var variant = ProductVariant.Create(tricky.Id, $"SKU-{Guid.NewGuid():N}"[..20]);
        var batch = InventoryBatch.Create(variant.Id, "Batch");
        db.Products.AddRange(tricky, plain);
        db.ProductVariants.Add(variant);
        db.InventoryBatches.Add(batch);
        db.DigitalInventoryCodes.AddRange(
            DigitalInventoryCode.Create(variant.Id, batch.Id, "CODE-1"),
            DigitalInventoryCode.Create(variant.Id, batch.Id, "CODE-2"));
        await db.SaveChangesAsync();

        var handler = new ExportProductsQueryHandler(db);
        var result = await handler.Handle(
            new ExportProductsQuery(new ExportProductsRequest([tricky.Id, plain.Id], null, null, null, false)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.EndsWith(".csv", result.Value.FileName);
        var lines = Encoding.UTF8.GetString(result.Value.Content).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToList();

        Assert.Equal("Name,Category,Price,Status,AvailableStock", lines[0]);
        Assert.Equal(3, lines.Count);
        Assert.Contains("\"Steam, \"\"Gift\"\" 20\",Category A,20,Draft,2", lines);
        Assert.Contains("Plain,Category A,5,Draft,0", lines);
    }

    [Fact]
    public async Task Export_WithNoSelection_IsRefused()
    {
        var db = TestCatalogDbContextFactory.Create();

        var result = await new ExportProductsQueryHandler(db).Handle(
            new ExportProductsQuery(new ExportProductsRequest(null, null, null, null, false)), CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
