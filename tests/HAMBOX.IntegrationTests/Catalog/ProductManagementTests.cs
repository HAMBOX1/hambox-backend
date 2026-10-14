using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;
using HAMBOX.Modules.Catalog.Application.Features.Products.ChangeProductCategory;
using HAMBOX.Modules.Catalog.Application.Features.Products.CreateProduct;
using HAMBOX.Modules.Catalog.Application.Features.Products.DeleteProduct;
using HAMBOX.Modules.Catalog.Application.Features.Products.UpdateProduct;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.IntegrationTests.Catalog;

/// <summary>
/// Product management (audit R11) against a real SQL Server (LocalDB): create, read back, edit price / name /
/// category / status, the lifecycle (publish, pause, archive, restore), bulk price rules, duplicate and delete —
/// the admin product screens are thin forms over exactly these handlers.
/// </summary>
public sealed class ProductManagementTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HamboxProductManagement_{Guid.NewGuid():N}";
    private string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    private CatalogDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(ConnectionString).Options, new PassthroughCodeProtector());

    private async Task<(Guid CategoryA, Guid CategoryB)> SeedCategoriesAsync()
    {
        await using var db = CreateContext();
        var a = Category.Create("أ", "Category A", $"cat-a-{Guid.NewGuid():N}");
        var b = Category.Create("ب", "Category B", $"cat-b-{Guid.NewGuid():N}");
        db.Categories.AddRange(a, b);
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    private async Task<Guid> CreateAsync(Guid categoryId, decimal price = 25m, string name = "Gift Card")
    {
        await using var db = CreateContext();
        var result = await new CreateProductCommandHandler(db).Handle(
            new CreateProductCommand(name + " AR", name, "وصف", "Description", price, categoryId), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        return result.Value;
    }

    private async Task<Product> LoadAsync(Guid id)
    {
        await using var db = CreateContext();
        return await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);
    }

    // ─── create / read ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_StoresTheProduct_AsDraft_WithItsPriceAndCategory()
    {
        var (categoryA, _) = await SeedCategoriesAsync();

        var id = await CreateAsync(categoryA, 19.99m, "PSN 20 USD");

        var product = await LoadAsync(id);
        Assert.Equal("PSN 20 USD", product.NameEn);
        Assert.Equal(19.99m, product.Price);
        Assert.Equal(categoryA, product.CategoryId);
        Assert.Equal(ProductStatus.Draft, product.Status);
    }

    [Fact]
    public async Task Create_WithAnUnknownCategory_IsRefused()
    {
        await using var db = CreateContext();

        var result = await new CreateProductCommandHandler(db).Handle(
            new CreateProductCommand("x", "Product", "d", "d", 10m, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.CategoryNotFound.Code, result.Error.Code);
        Assert.Equal(0, await db.Products.CountAsync());
    }

    // ─── edit ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesNamePriceCategoryAndStatus_Together()
    {
        var (categoryA, categoryB) = await SeedCategoriesAsync();
        var id = await CreateAsync(categoryA);

        await using (var db = CreateContext())
        {
            var result = await new UpdateProductCommandHandler(db, new FixedCurrentUser("admin-1")).Handle(
                new UpdateProductCommand(id, "اسم", "Renamed", "وصف", "New description", 31.5m, categoryB, ProductStatus.Active),
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        }

        var product = await LoadAsync(id);
        Assert.Equal("Renamed", product.NameEn);
        Assert.Equal(31.5m, product.Price);
        Assert.Equal(categoryB, product.CategoryId);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public async Task Update_OfAMissingProduct_IsNotFound()
    {
        var (categoryA, _) = await SeedCategoriesAsync();
        await using var db = CreateContext();

        var result = await new UpdateProductCommandHandler(db, new FixedCurrentUser("admin-1")).Handle(
            new UpdateProductCommand(Guid.NewGuid(), "x", "x", "d", "d", 1m, categoryA, ProductStatus.Draft), CancellationToken.None);

        Assert.Equal(CatalogErrors.ProductNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task ChangeCategory_MovesTheProduct_AndRefusesAnUnknownCategory()
    {
        var (categoryA, categoryB) = await SeedCategoriesAsync();
        var id = await CreateAsync(categoryA);

        await using (var db = CreateContext())
        {
            var ok = await new ChangeProductCategoryCommandHandler(db).Handle(
                new ChangeProductCategoryCommand(id, categoryB), CancellationToken.None);
            Assert.True(ok.IsSuccess);
        }

        Assert.Equal(categoryB, (await LoadAsync(id)).CategoryId);

        await using var db2 = CreateContext();
        var bad = await new ChangeProductCategoryCommandHandler(db2).Handle(
            new ChangeProductCategoryCommand(id, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(CatalogErrors.CategoryNotFound.Code, bad.Error.Code);
        Assert.Equal(categoryB, (await LoadAsync(id)).CategoryId);
    }

    // ─── price rules (single product; the bulk screen applies the same handler per row) ──

    [Theory]
    [InlineData(PriceAdjustmentMode.IncreasePercent, 10, 110.00)]
    [InlineData(PriceAdjustmentMode.DecreasePercent, 25, 75.00)]
    [InlineData(PriceAdjustmentMode.SetFixed, 42.5, 42.50)]
    public async Task AdjustPrice_AppliesTheRule_AndRoundsToTwoDecimals(PriceAdjustmentMode mode, double value, double expected)
    {
        var (categoryA, _) = await SeedCategoriesAsync();
        var id = await CreateAsync(categoryA, 100m);

        await using (var db = CreateContext())
        {
            var result = await new AdjustProductPriceCommandHandler(db).Handle(
                new AdjustProductPriceCommand(id, mode, (decimal)value), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        Assert.Equal((decimal)expected, (await LoadAsync(id)).Price);
    }

    [Fact]
    public async Task AdjustPrice_ThatWouldGoNegative_IsRefused_AndThePriceIsUntouched()
    {
        var (categoryA, _) = await SeedCategoriesAsync();
        var id = await CreateAsync(categoryA, 10m);

        await using var db = CreateContext();
        var result = await new AdjustProductPriceCommandHandler(db).Handle(
            new AdjustProductPriceCommand(id, PriceAdjustmentMode.DecreasePercent, 150m), CancellationToken.None);

        Assert.Equal(CatalogErrors.InvalidPriceAdjustment.Code, result.Error.Code);
        Assert.Equal(10m, (await LoadAsync(id)).Price);
    }

    // ─── lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public async Task Lifecycle_PublishThenPauseThenArchive_AndEachInvalidStepIsRefused()
    {
        var (categoryA, _) = await SeedCategoriesAsync();
        var id = await CreateAsync(categoryA);

        await using (var db = CreateContext())
        {
            Assert.True((await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(id), CancellationToken.None)).IsSuccess);
        }

        Assert.Equal(ProductStatus.Active, (await LoadAsync(id)).Status);

        await using (var db = CreateContext())
        {
            var again = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(id), CancellationToken.None);
            Assert.True(again.IsFailure); // already active
        }

        await using (var db = CreateContext())
        {
            Assert.True((await new DeactivateProductCommandHandler(db).Handle(new DeactivateProductCommand(id), CancellationToken.None)).IsSuccess);
        }

        Assert.Equal(ProductStatus.Inactive, (await LoadAsync(id)).Status);

        await using (var db = CreateContext())
        {
            Assert.True((await new ArchiveProductCommandHandler(db).Handle(new ArchiveProductCommand(id), CancellationToken.None)).IsSuccess);
        }

        Assert.Equal(ProductStatus.Archived, (await LoadAsync(id)).Status);

        await using (var db = CreateContext())
        {
            var publishArchived = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(id), CancellationToken.None);
            Assert.True(publishArchived.IsFailure);
        }
    }

    // ─── duplicate / delete ─────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_CreatesACopy_WithTheSamePriceAndCategory_UnderANewName()
    {
        var (categoryA, _) = await SeedCategoriesAsync();
        var id = await CreateAsync(categoryA, 40m, "Steam 40");

        Guid copyId;
        await using (var db = CreateContext())
        {
            var result = await new DuplicateProductCommandHandler(db, new FixedCurrentUser("admin-1")).Handle(
                new DuplicateProductCommand(id, null), CancellationToken.None);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
            copyId = result.Value;
        }

        var copy = await LoadAsync(copyId);
        Assert.NotEqual(id, copyId);
        Assert.Equal("Steam 40 (Copy)", copy.NameEn);
        Assert.Equal(40m, copy.Price);
        Assert.Equal(categoryA, copy.CategoryId);
    }

    [Fact]
    public async Task Delete_RemovesTheProductFromTheCatalog_AndMissingOnesAreNotFound()
    {
        var (categoryA, _) = await SeedCategoriesAsync();
        var id = await CreateAsync(categoryA);

        await using (var db = CreateContext())
        {
            var result = await new DeleteProductCommandHandler(db, new RecordingFileStorage()).Handle(
                new DeleteProductCommand(id), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        await using var verify = CreateContext();
        Assert.False(await verify.Products.AnyAsync(p => p.Id == id)); // gone from every normal query

        var again = await new DeleteProductCommandHandler(verify, new RecordingFileStorage()).Handle(
            new DeleteProductCommand(id), CancellationToken.None);
        Assert.Equal(CatalogErrors.ProductNotFound.Code, again.Error.Code);
    }

    // ─── doubles ────────────────────────────────────────────────────────

    private sealed class PassthroughCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;
        public string Unprotect(string cipherText) => cipherText;
    }

    private sealed class FixedCurrentUser(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
        public string? DisplayName => null;
        public bool IsAuthenticated => UserId is not null;
        public bool IsAdminContext => UserId is not null;
        public Guid? SessionId => null;
    }

    private sealed class RecordingFileStorage : IFileStorage
    {
        public long MaxFileSizeBytes => 10_000_000;

        public bool IsAllowedContentType(string contentType) => true;

        public Task<StoredFileResult> SaveAsync(
            Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
