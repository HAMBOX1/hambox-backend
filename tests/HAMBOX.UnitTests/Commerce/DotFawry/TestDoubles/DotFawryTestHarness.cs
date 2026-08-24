using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Commerce.DotFawry.TestDoubles;

/// <summary>
/// Wires every DOT Fawry handler/service against the same in-memory db contexts and fakes, so
/// tests can exercise initiate -&gt; notification -&gt; verify end-to-end without repeating ~30 lines
/// of DI wiring per test. A sibling of <c>DotTestHarness</c> (carrier-billing OTP product) — kept
/// separate rather than shared, matching the production code's own separation.
/// </summary>
internal sealed class DotFawryTestHarness
{
    public required CommerceDbContext CommerceDb { get; init; }
    public required CatalogDbContext CatalogDb { get; init; }
    public required FakeCurrentUserService CurrentUser { get; init; }
    public required FakeInventoryEngine InventoryEngine { get; init; }
    public required FakeDotFawryPaymentGateway Gateway { get; init; }
    public required FakeDotFawryChargeAmountResolver ChargeAmountResolver { get; init; }
    public required FakeCommunicationService Communication { get; init; }
    public required FakeOperationalJobQueue JobQueue { get; init; }
    public required DotFawrySettings Settings { get; init; }
    public required InitiateDotFawryCheckoutCommandHandler InitiateHandler { get; init; }
    public required DotFawryPaymentVerificationService VerificationService { get; init; }
    public required HandleDotFawryNotificationCommandHandler NotificationHandler { get; init; }
    public required GetDotFawryPaymentStatusQueryHandler StatusQueryHandler { get; init; }

    public static DotFawryTestHarness Create(string? userId = "user-1", DotFawrySettings? settingsOverride = null)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var currentUser = new FakeCurrentUserService(userId);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var gateway = new FakeDotFawryPaymentGateway();
        var chargeAmountResolver = new FakeDotFawryChargeAmountResolver();
        var communication = new FakeCommunicationService();
        var jobQueue = new FakeOperationalJobQueue();
        var platformSettings = new FakeDotPlatformSettingsProvider();

        var settings = settingsOverride ?? new DotFawrySettings
        {
            BaseUrl = "https://dot-jo.biz",
            PartnerId = "partner_test",
            ServiceId = "1",
            OperatorId = "141",
            Username = "test-user",
            Password = "test-pass",
        };
        var dotFawryOptions = Options.Create(settings);

        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        var fulfillmentRouter = new FakeFulfillmentRouter();
        var cartLineValidator = new CartLineValidator(inventoryEngine, fulfillmentRouter);
        var membershipAccess = new FakeMembershipAccessProvider();

        var transactionService = new FakeCommerceTransactionService();
        var fulfillmentService = new OrderFulfillmentService(
            commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), fulfillmentRouter, NullLogger<OrderFulfillmentService>.Instance);
        var promotionRedemptionService = new PromotionRedemptionService(commerceDb);
        var referralRewardService = new ReferralRewardService(commerceDb, new FakeMembershipEngine());
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb, platformSettings, referralRewardService, communication, NullLogger<ReferralLifecycleService>.Instance);

        var verificationService = new DotFawryPaymentVerificationService(
            commerceDb,
            catalogDb,
            transactionService,
            gateway,
            fulfillmentService,
            promotionRedemptionService,
            referralLifecycle,
            communication,
            jobQueue,
            NullLogger<DotFawryPaymentVerificationService>.Instance);

        var initiateHandler = new InitiateDotFawryCheckoutCommandHandler(
            commerceDb,
            catalogDb,
            currentUser,
            inventoryEngine,
            cartResponseBuilder,
            cartLineValidator,
            membershipAccess,
            chargeAmountResolver,
            gateway,
            dotFawryOptions,
            platformSettings,
            verificationService,
            NullLogger<InitiateDotFawryCheckoutCommandHandler>.Instance);

        var notificationHandler = new HandleDotFawryNotificationCommandHandler(
            commerceDb, verificationService, NullLogger<HandleDotFawryNotificationCommandHandler>.Instance);

        var statusQueryHandler = new GetDotFawryPaymentStatusQueryHandler(commerceDb, currentUser);

        return new DotFawryTestHarness
        {
            CommerceDb = commerceDb,
            CatalogDb = catalogDb,
            CurrentUser = currentUser,
            InventoryEngine = inventoryEngine,
            Gateway = gateway,
            ChargeAmountResolver = chargeAmountResolver,
            Communication = communication,
            JobQueue = jobQueue,
            Settings = settings,
            InitiateHandler = initiateHandler,
            VerificationService = verificationService,
            NotificationHandler = notificationHandler,
            StatusQueryHandler = statusQueryHandler,
        };
    }

    public async Task<(Product Product, ProductVariant Variant)> SeedProductAsync(int stock, decimal price = 19.99m)
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", price, category.Id);
        product.Activate();
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();

        CatalogDb.Categories.Add(category);
        CatalogDb.Products.Add(product);
        CatalogDb.ProductVariants.Add(variant);
        await CatalogDb.SaveChangesAsync(CancellationToken.None);

        InventoryEngine.AvailableStockByVariant[variant.Id] = stock;

        return (product, variant);
    }

    private ShoppingCart? _cart;

    public async Task SeedCartAsync(Product product, ProductVariant variant, int quantity = 1)
    {
        if (_cart is null)
        {
            _cart = ShoppingCart.CreateForUser(CurrentUser.UserId!);
            CommerceDb.ShoppingCarts.Add(_cart);
        }

        _cart.AddOrUpdateItem(product.Id, quantity, product.Price, variant.Id);
        await CommerceDb.SaveChangesAsync(CancellationToken.None);
    }
}
