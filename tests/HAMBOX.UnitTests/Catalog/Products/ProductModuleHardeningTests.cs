using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Features.Instructions.SaveProductInstructions;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport;
using HAMBOX.Modules.Catalog.Application.Features.ImportExport.ExportCatalog;
using HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;
using HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;
using HAMBOX.Modules.Catalog.Application.Features.Products.CreateProduct;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.GetProductImages;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Catalog.Products;

/// <summary>
/// Regression tests for the Products-module QA findings: unpublished products must never be visible to
/// non-admins (C1), CSV cells can't carry spreadsheet formulas (H1), bad bulk input is a clean failure
/// (H3) and a product can only go live — and stay live — with a price and an active variant (H4).
/// </summary>
public sealed class ProductModuleHardeningTests
{
    private sealed class NoSupplierRouter : IFulfillmentRouter
    {
        public Task<FulfillmentReadiness> GetReadinessAsync(Guid variantId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, FulfillmentReadiness>> GetReadinessBulkAsync(
            IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, FulfillmentReadiness>>(new Dictionary<Guid, FulfillmentReadiness>());

        public Task<IReadOnlyDictionary<Guid, decimal>> GetEffectivePriceOverridesBulkAsync(
            IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(new Dictionary<Guid, decimal>());
    }

    /// <summary>Staff = an admin holding Catalog.Products.View; "costs" additionally holds ViewCosts.</summary>
    private static FakeCurrentUserPermissions StaffPermissions(bool costs = false) => costs
        ? new FakeCurrentUserPermissions(CatalogPermissions.ProductsView, CatalogPermissions.ViewCosts)
        : new FakeCurrentUserPermissions(CatalogPermissions.ProductsView);

    private static async Task<(TestCatalogDbContext Db, Product Active, Product Draft, Product Inactive)> SeedAsync()
    {
        var db = TestCatalogDbContextFactory.Create();
        var category = Category.Create("ف", "Cat", $"cat-{Guid.NewGuid():N}");
        db.Categories.Add(category);

        var active = Product.Create("نشط", "Live", "d", "d", 10m, category.Id);
        active.Activate();
        var draft = Product.Create("مسودة", "Draft", "d", "d", 10m, category.Id);
        var inactive = Product.Create("غير", "Inactive", "d", "d", 10m, category.Id);
        inactive.Activate();
        inactive.Deactivate();
        db.Products.AddRange(active, draft, inactive);
        await db.SaveChangesAsync();
        return (db, active, draft, inactive);
    }

    private static async Task AddActiveVariantAsync(TestCatalogDbContext db, Product product, decimal? priceOverride = null)
    {
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}", priceOverride: priceOverride);
        variant.Activate();
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
    }

    // ---------- C1: non-admins only ever see Active products ----------

    [Theory]
    [InlineData(null)]
    [InlineData(ProductStatus.Draft)]
    [InlineData(ProductStatus.Inactive)]
    [InlineData(ProductStatus.Archived)]
    public async Task ProductList_Anonymous_OnlySeesActive_NoMatterWhichStatusIsRequested(ProductStatus? requested)
    {
        var (db, active, _, _) = await SeedAsync();
        var handler = new GetProductsQueryHandler(
            db, new FakeCurrentUserService(null), FakeCurrentUserPermissions.None, new FakeMembershipAccessProvider(), new NoSupplierRouter(),
            NullLogger<GetProductsQueryHandler>.Instance);

        var result = await handler.Handle(new GetProductsQuery(1, 50, null, requested, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value.Items);
        Assert.Equal(active.Id, only.Id);
    }

    [Fact]
    public async Task ProductList_Admin_StillSeesEveryStatus()
    {
        var (db, _, _, _) = await SeedAsync();
        var handler = new GetProductsQueryHandler(
            db, new FakeCurrentUserService("admin-1"), StaffPermissions(), new FakeMembershipAccessProvider(), new NoSupplierRouter(),
            NullLogger<GetProductsQueryHandler>.Instance);

        var result = await handler.Handle(new GetProductsQuery(1, 50, null, null, null, null), CancellationToken.None);

        Assert.Equal(3, result.Value.Items.Count);
    }

    [Fact]
    public async Task ProductById_Anonymous_GetsNotFoundForDraft_ButAdminCanOpenIt()
    {
        var (db, _, draft, _) = await SeedAsync();

        var anonymous = await new GetProductByIdQueryHandler(db, new FakeCurrentUserService(null), FakeCurrentUserPermissions.None, new FakeMembershipAccessProvider())
            .Handle(new GetProductByIdQuery(draft.Id), CancellationToken.None);
        var admin = await new GetProductByIdQueryHandler(db, new FakeCurrentUserService("admin-1"), StaffPermissions(), new FakeMembershipAccessProvider())
            .Handle(new GetProductByIdQuery(draft.Id), CancellationToken.None);

        Assert.True(anonymous.IsFailure);
        Assert.True(admin.IsSuccess);
    }

    // ---------- H1: CSV formula injection ----------

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("Plain name", "Plain name")]
    [InlineData("Has, comma", "\"Has, comma\"")]
    [InlineData("", "")]
    public void CsvCell_NeutralisesFormulaPrefixes_AndQuotesAsNeeded(string input, string expected) =>
        Assert.Equal(expected, CsvCell.Escape(input));

    [Fact]
    public void CsvCell_NullBecomesEmpty() => Assert.Equal(string.Empty, CsvCell.Escape(null));

    // ---------- H4: publish needs a price and an active variant ----------

    [Fact]
    public async Task Publish_WithoutVariant_IsRejected_AndStaysDraft()
    {
        var (db, _, draft, _) = await SeedAsync();

        var result = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(draft.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Products.NotPublishableNoVariant", result.Error.Code);
        Assert.Equal(ProductStatus.Draft, (await db.Products.FindAsync(draft.Id))!.Status);
    }

    [Fact]
    public async Task Publish_WithZeroPrice_IsRejected()
    {
        var (db, _, draft, _) = await SeedAsync();
        draft.ChangePrice(0m);
        await AddActiveVariantAsync(db, draft);

        var result = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(draft.Id), CancellationToken.None);

        Assert.Equal("Products.NotPublishablePrice", result.Error.Code);
    }

    [Fact]
    public async Task Publish_ZeroBasePriceButPricedVariant_IsAllowed()
    {
        var (db, _, draft, _) = await SeedAsync();
        draft.ChangePrice(0m);
        await AddActiveVariantAsync(db, draft, priceOverride: 25m);

        var result = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(draft.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Publish_WithPriceAndActiveVariant_GoesLive()
    {
        var (db, _, draft, _) = await SeedAsync();
        await AddActiveVariantAsync(db, draft);

        var result = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(draft.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Active, (await db.Products.FindAsync(draft.Id))!.Status);
    }

    [Fact]
    public async Task Publish_WithOnlyDraftVariants_GoesLive_AndActivatesThem()
    {
        var (db, _, draft, _) = await SeedAsync();
        var variant = ProductVariant.Create(draft.Id, $"SKU-{Guid.NewGuid():N}");
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        var result = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(draft.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductVariantStatus.Active, (await db.ProductVariants.FindAsync(variant.Id))!.Status);
    }

    [Fact]
    public async Task Publish_WithOnlyArchivedVariants_IsRejected()
    {
        var (db, _, draft, _) = await SeedAsync();
        var variant = ProductVariant.Create(draft.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Archive();
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        var result = await new PublishProductCommandHandler(db).Handle(new PublishProductCommand(draft.Id), CancellationToken.None);

        Assert.Equal("Products.NotPublishableNoVariant", result.Error.Code);
    }

    [Fact]
    public async Task Create_AsActive_IsRejected_BecauseANewProductHasNoVariants()
    {
        var (db, _, _, _) = await SeedAsync();
        var categoryId = db.Categories.First().Id;
        var handler = new CreateProductCommandHandler(db);

        var result = await handler.Handle(
            new CreateProductCommand("", "New", "d", "d", 5m, categoryId, Status: ProductStatus.Active), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AdjustPrice_CannotZeroALiveProduct_ButCanZeroADraft()
    {
        var (db, active, draft, _) = await SeedAsync();
        var handler = new AdjustProductPriceCommandHandler(db);

        var live = await handler.Handle(new AdjustProductPriceCommand(active.Id, PriceAdjustmentMode.DecreasePercent, 100), CancellationToken.None);
        var notLive = await handler.Handle(new AdjustProductPriceCommand(draft.Id, PriceAdjustmentMode.DecreasePercent, 100), CancellationToken.None);

        Assert.Equal("Products.NotPublishablePrice", live.Error.Code);
        Assert.True(notLive.IsSuccess);
    }

    [Fact]
    public async Task AdjustPrice_AboveTheCeiling_IsRejected()
    {
        var (db, _, draft, _) = await SeedAsync();

        var result = await new AdjustProductPriceCommandHandler(db).Handle(
            new AdjustProductPriceCommand(draft.Id, PriceAdjustmentMode.SetFixed, ProductPublishGuard.MaxPrice + 1), CancellationToken.None);

        Assert.Equal("Products.PriceTooHigh", result.Error.Code);
    }

    [Fact]
    public void PriceValidators_RejectAbsurdPrices()
    {
        var tooHigh = new CreateProductCommand("", "N", "d", "d", 100000000000000000000m, Guid.NewGuid());
        Assert.False(new CreateProductCommandValidator().Validate(tooHigh).IsValid);

        var ok = new CreateProductCommand("", "N", "d", "d", 19.99m, Guid.NewGuid());
        Assert.True(new CreateProductCommandValidator().Validate(ok).IsValid);

        Assert.False(new AdjustProductPriceCommandValidator()
            .Validate(new AdjustProductPriceCommand(Guid.NewGuid(), PriceAdjustmentMode.IncreasePercent, 99999999999999999m)).IsValid);
    }

    // ---------- H3: bad bulk input fails cleanly instead of crashing ----------

    private static BulkProductsCommand Bulk(IReadOnlyList<Guid> ids, string? action, string? badgeEn = null, decimal? value = null,
        PriceAdjustmentMode? mode = null) =>
        new(new BulkProductsRequest(ids, null, null, null, false, action!, null, mode, value, BadgeTextEn: badgeEn));

    [Fact]
    public async Task Bulk_NullAction_IsAFailure_NotAnException()
    {
        var (db, active, _, _) = await SeedAsync();
        var handler = new BulkProductsCommandHandler(db, null!, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(Bulk([active.Id], null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Bulk_BadgeLongerThanTheColumn_IsRejectedUpFront()
    {
        var (db, active, _, _) = await SeedAsync();
        var handler = new BulkProductsCommandHandler(db, null!, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(Bulk([active.Id], "set-badge", badgeEn: new string('B', 61)), CancellationToken.None);

        Assert.Equal("Products.BadgeTooLong", result.Error.Code);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(100000000)]
    public async Task Bulk_AdjustPrice_OutOfRangeValue_IsRejected(double value)
    {
        var (db, active, _, _) = await SeedAsync();
        var handler = new BulkProductsCommandHandler(db, null!, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            Bulk([active.Id], "adjust-price", value: (decimal)value, mode: PriceAdjustmentMode.IncreasePercent), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Bulk_AdjustPrice_NeverZeroesALiveProduct_AndReportsWhy()
    {
        var (db, active, draft, _) = await SeedAsync();
        var handler = new BulkProductsCommandHandler(db, null!, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            Bulk([active.Id, draft.Id], "adjust-price", value: 100m, mode: PriceAdjustmentMode.DecreasePercent), CancellationToken.None);

        Assert.Equal(1, result.Value.SuccessCount);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Equal(10m, (await db.Products.FindAsync(active.Id))!.Price);
    }

    // ---------- Medium fixes ----------

    [Fact]
    public async Task Restore_ArchivedProduct_ComesBackAsDraft_AndRejectsAProductThatIsNotArchived()
    {
        var (db, active, draft, _) = await SeedAsync();
        draft.Archive();
        await db.SaveChangesAsync();
        var handler = new RestoreProductCommandHandler(db);

        var restored = await handler.Handle(new RestoreProductCommand(draft.Id), CancellationToken.None);
        var notArchived = await handler.Handle(new RestoreProductCommand(active.Id), CancellationToken.None);

        Assert.True(restored.IsSuccess);
        Assert.Equal(ProductStatus.Draft, (await db.Products.FindAsync(draft.Id))!.Status);
        Assert.True(notArchived.IsFailure);
    }

    [Fact]
    public async Task ProductList_Anonymous_PageSizeIsCapped_AdminIsNot()
    {
        var (db, _, _, _) = await SeedAsync();
        var categoryId = db.Categories.First().Id;
        for (var i = 0; i < 110; i++)
        {
            var product = Product.Create("م", $"Bulk {i}", "d", "d", 5m, categoryId);
            product.Activate();
            db.Products.Add(product);
        }

        await db.SaveChangesAsync();

        GetProductsQueryHandler Handler(string? userId) => new(
            db, new FakeCurrentUserService(userId), userId is null ? FakeCurrentUserPermissions.None : StaffPermissions(), new FakeMembershipAccessProvider(), new NoSupplierRouter(),
            NullLogger<GetProductsQueryHandler>.Instance);

        var anonymous = await Handler(null).Handle(new GetProductsQuery(1, 100000, null, null, null, null), CancellationToken.None);
        var anonymousAll = await Handler(null).Handle(new GetProductsQuery(1, -1, null, null, null, null), CancellationToken.None);
        var admin = await Handler("admin-1").Handle(new GetProductsQuery(1, 100000, null, null, null, null), CancellationToken.None);

        Assert.Equal(100, anonymous.Value.Items.Count);
        Assert.Equal(100, anonymousAll.Value.Items.Count);
        Assert.True(admin.Value.Items.Count > 100);
    }

    [Theory]
    [InlineData("😀")]
    [InlineData("!!!")]
    public async Task Search_WithOnlySymbols_MatchesNothing_InsteadOfEverything(string term)
    {
        var (db, _, _, _) = await SeedAsync();

        var found = ProductQueryFilters.ApplyBaseFilters(db.Products, term, null, null, db: db).ToList();

        Assert.Empty(found);
    }

    [Fact]
    public async Task ProductImages_Anonymous_CannotSeeADraftsImages_ButAdminCan()
    {
        var (db, active, draft, _) = await SeedAsync();

        var anonymousDraft = await new GetProductImagesQueryHandler(db, FakeCurrentUserPermissions.None)
            .Handle(new GetProductImagesQuery(draft.Id), CancellationToken.None);
        var anonymousActive = await new GetProductImagesQueryHandler(db, FakeCurrentUserPermissions.None)
            .Handle(new GetProductImagesQuery(active.Id), CancellationToken.None);
        var adminDraft = await new GetProductImagesQueryHandler(db, StaffPermissions())
            .Handle(new GetProductImagesQuery(draft.Id), CancellationToken.None);

        Assert.True(anonymousDraft.IsFailure);
        Assert.True(anonymousActive.IsSuccess);
        Assert.True(adminDraft.IsSuccess);
    }

    [Fact]
    public void PriceValidators_RejectMoreThanTwoDecimals()
    {
        var threeDecimals = new CreateProductCommand("", "N", "d", "d", 1.239m, Guid.NewGuid());

        Assert.False(new CreateProductCommandValidator().Validate(threeDecimals).IsValid);
    }

    // ---------- Field-level permissions ----------

    [Fact]
    public async Task ProductList_AdminTokenWithoutCatalogPermission_IsTreatedLikeThePublic()
    {
        var (db, active, _, _) = await SeedAsync();
        // An admin-portal token that holds no Catalog.Products.View (e.g. a Dashboard-only role).
        var handler = new GetProductsQueryHandler(
            db, new FakeCurrentUserService("dashboard-only"), FakeCurrentUserPermissions.None, new FakeMembershipAccessProvider(),
            new NoSupplierRouter(), NullLogger<GetProductsQueryHandler>.Instance);

        var result = await handler.Handle(new GetProductsQuery(1, 50, null, ProductStatus.Draft, null, null), CancellationToken.None);

        Assert.Equal(active.Id, Assert.Single(result.Value.Items).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductList_CostTiers_AreOnlyShownToStaffHoldingViewCosts(bool holdsViewCosts)
    {
        var (db, active, _, _) = await SeedAsync();
        var variant = ProductVariant.Create(active.Id, $"SKU-{Guid.NewGuid():N}");
        variant.SetCostPrice(4.25m);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        var handler = new GetProductsQueryHandler(
            db, new FakeCurrentUserService("admin-1"), StaffPermissions(costs: holdsViewCosts), new FakeMembershipAccessProvider(),
            new NoSupplierRouter(), NullLogger<GetProductsQueryHandler>.Instance);

        var result = await handler.Handle(new GetProductsQuery(1, 50, null, ProductStatus.Active, null, null), CancellationToken.None);

        var tiers = result.Value.Items.Single(p => p.Id == active.Id).PriceTiers;
        Assert.NotNull(tiers);
        Assert.Equal(holdsViewCosts ? 4.25m : null, tiers!.CostMin);
        Assert.NotNull(tiers.SaleMin); // the non-cost tiers stay visible to staff either way
    }

    [Fact]
    public void CatalogPermissionNames_MatchTheIdentityPermissionConstants()
    {
        Assert.Equal(PermissionConstants.Catalog.Products.View, CatalogPermissions.ProductsView);
        Assert.Equal(PermissionConstants.Catalog.Inventory.ViewCosts, CatalogPermissions.ViewCosts);
    }

    [Theory]
    [InlineData(true, false, "Catalog.ExportCodesNotPermitted")]
    [InlineData(false, true, "Catalog.ExportCostsNotPermitted")]
    public async Task CatalogExport_WithoutRevealOrCostPermission_IsRefused_BeforeAnyJobIsCreated(bool codes, bool suppliers, string expectedError)
    {
        var (db, _, _, _) = await SeedAsync();
        var handler = new ExportCatalogCommandHandler(db, null!, null!, StaffPermissions());
        var options = new CatalogPackageOptions(IncludeDigitalCodes: codes, IncludeSuppliers: suppliers);

        var result = await handler.Handle(
            new ExportCatalogCommand(new CatalogExportScope(null, null, null, null, false, true), CatalogPackageFormat.Hambox, options, false, false, null),
            CancellationToken.None);

        Assert.Equal(expectedError, result.Error.Code);
        Assert.Empty(db.CatalogPackageJobs);
    }

    [Theory]
    [InlineData(200_000, true)]
    [InlineData(200_001, false)]
    public void SaveInstructions_ContentOverTheCap_IsRejected(int length, bool valid)
    {
        var command = new SaveProductInstructionsCommand(Guid.NewGuid(), null, "How to activate", new string('x', length));

        Assert.Equal(valid, new SaveProductInstructionsCommandValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(9.99, 4.99, true)]
    [InlineData(0.0, null, false)]
    [InlineData(null, 0.0, false)]
    [InlineData(-1.0, null, false)]
    [InlineData(10_000_001.0, null, false)]
    public void UpdateVariant_PriceOverrideAndMemberPrice_MustBePositive(double? priceOverride, double? memberPrice, bool valid)
    {
        var command = new UpdateProductVariantCommand(
            Guid.NewGuid(), "SKU-1", null, (decimal?)priceOverride, null, 0, ProductVariantStatus.Active, true, null, 5, [],
            null, (decimal?)memberPrice);

        Assert.Equal(valid, new UpdateProductVariantCommandValidator().Validate(command).IsValid);
    }
}
