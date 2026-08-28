using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Commerce.Dot.TestDoubles;

/// <summary>Wires every DOT handler/service against the same in-memory db contexts and fakes, so
/// tests can exercise initiate -&gt; callback/notification -&gt; verify end-to-end without repeating
/// ~30 lines of DI wiring per test.</summary>
internal sealed class DotTestHarness
{
    public required CommerceDbContext CommerceDb { get; init; }
    public required CatalogDbContext CatalogDb { get; init; }
    public required FakeCurrentUserService CurrentUser { get; init; }
    public required FakeInventoryEngine InventoryEngine { get; init; }
    public required FakeDotPaymentGateway Gateway { get; init; }
    public required FakeDotPricePointResolver PriceResolver { get; init; }
    public required FakeCommunicationService Communication { get; init; }
    public required FakeOperationalJobQueue JobQueue { get; init; }
    public required DotSettings Settings { get; init; }
    public required InitiateDotCheckoutCommandHandler InitiateHandler { get; init; }
    public required DotPaymentVerificationService VerificationService { get; init; }
    public required HandleDotRedirectCallbackCommandHandler CallbackHandler { get; init; }
    public required HandleDotNotificationCommandHandler NotificationHandler { get; init; }
    public required GetDotPaymentStatusQueryHandler StatusQueryHandler { get; init; }

    public static DotTestHarness Create(string? userId = "user-1", DotSettings? settingsOverride = null)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var currentUser = new FakeCurrentUserService(userId);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var gateway = new FakeDotPaymentGateway();
        var priceResolver = new FakeDotPricePointResolver();
        var communication = new FakeCommunicationService();
        var jobQueue = new FakeOperationalJobQueue();
        var platformSettings = new FakeDotPlatformSettingsProvider();

        var settings = settingsOverride ?? new DotSettings
        {
            BaseUrl = "https://dot-jo.biz",
            PartnerId = "partner_test",
            ServiceId = "1",
            Username = "test-user",
            Password = "test-pass",
            PublicRedirectUrl = "https://hambox.test/api/payments/dot/callback",
            FrontendResultUrl = "https://hambox.test/checkout/dot/result",
        };
        var dotOptions = Options.Create(settings);

        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        var fulfillmentRouter = new FakeFulfillmentRouter();
        var cartLineValidator = new CartLineValidator(inventoryEngine, fulfillmentRouter, new NullSupplierPricingEngine());
        var membershipAccess = new FakeMembershipAccessProvider();

        var initiateHandler = new InitiateDotCheckoutCommandHandler(
            commerceDb,
            catalogDb,
            LegalTestDbContextFactory.Create(),
            currentUser,
            inventoryEngine,
            cartResponseBuilder,
            cartLineValidator,
            membershipAccess,
            priceResolver,
            gateway,
            dotOptions,
            platformSettings,
            NullLogger<InitiateDotCheckoutCommandHandler>.Instance);

        var transactionService = new FakeCommerceTransactionService();
        var fulfillmentService = new OrderFulfillmentService(
            commerceDb, inventoryEngine, new NullSupplierFulfillmentService(), fulfillmentRouter,
            new NullSupplierPricingEngine(), HAMBOX.UnitTests.Suppliers.TestDoubles.SuppliersTestDbContextFactory.Create(),
            NullLogger<OrderFulfillmentService>.Instance);
        var promotionRedemptionService = new PromotionRedemptionService(commerceDb);
        var referralRewardService = new ReferralRewardService(commerceDb, new FakeMembershipEngine());
        var referralLifecycle = new ReferralLifecycleService(
            commerceDb, platformSettings, referralRewardService, communication, NullLogger<ReferralLifecycleService>.Instance);

        var verificationService = new DotPaymentVerificationService(
            commerceDb,
            catalogDb,
            transactionService,
            gateway,
            fulfillmentService,
            promotionRedemptionService,
            referralLifecycle,
            communication,
            jobQueue,
            NullLogger<DotPaymentVerificationService>.Instance);

        var callbackHandler = new HandleDotRedirectCallbackCommandHandler(
            commerceDb, verificationService, NullLogger<HandleDotRedirectCallbackCommandHandler>.Instance);

        var notificationHandler = new HandleDotNotificationCommandHandler(
            commerceDb, verificationService, NullLogger<HandleDotNotificationCommandHandler>.Instance);

        var statusQueryHandler = new GetDotPaymentStatusQueryHandler(commerceDb, currentUser);

        return new DotTestHarness
        {
            CommerceDb = commerceDb,
            CatalogDb = catalogDb,
            CurrentUser = currentUser,
            InventoryEngine = inventoryEngine,
            Gateway = gateway,
            PriceResolver = priceResolver,
            Communication = communication,
            JobQueue = jobQueue,
            Settings = settings,
            InitiateHandler = initiateHandler,
            VerificationService = verificationService,
            CallbackHandler = callbackHandler,
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

    /// <summary>Reuses the same tracked cart instance across calls (mirrors the real
    /// one-cart-per-user invariant) so seeding a second order for the same user after the first
    /// one already cleared its cart doesn't leave two ShoppingCart rows behind.</summary>
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
