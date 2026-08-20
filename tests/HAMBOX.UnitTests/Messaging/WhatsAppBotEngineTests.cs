using HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryTree;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;
using HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Services;
using HAMBOX.Modules.Messaging.Domain.Conversations;
using HAMBOX.Modules.Messaging.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Infrastructure.Providers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Messaging.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Messaging;

/// <summary>
/// Drives the FakeWhatsAppProvider through the complete menu-driven flow — Language → Main → Browse
/// Categories → Products → Product Detail → Variant → Add to Cart — against real Catalog/Commerce
/// handlers (InMemory DbContexts), proving the bot engine reuses existing services end to end rather
/// than reimplementing catalog/pricing/stock/cart logic.
/// </summary>
public sealed class WhatsAppBotEngineTests
{
    private static (Category Category, Product Product, ProductVariant Variant) SeedCatalog(
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb) =>
        MessagingTestFixtures.SeedCatalog(catalogDb);

    private static WhatsAppBotEngine CreateEngine(
        MessagingDbContext messagingDb,
        MultiHandlerFakeSender sender,
        FakeWhatsAppProvider provider) =>
        MessagingTestFixtures.CreateEngine(messagingDb, sender, provider);

    [Fact]
    public async Task Browse_Product_Variant_Cart_FlowSucceeds()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (category, product, variant) = SeedCatalog(catalogDb);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 5;

        var guestUser = new FakeCurrentUserService(userId: null);
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), guestUser);

        var sender = new MultiHandlerFakeSender(
            new GetCategoryTreeQueryHandler(catalogDb),
            new GetProductsQueryHandler(catalogDb, guestUser, new FakeMembershipAccessProvider(), NullLogger<GetProductsQueryHandler>.Instance),
            new GetProductByIdQueryHandler(catalogDb, guestUser, new FakeMembershipAccessProvider()),
            new GetStorefrontProductConfigurationsQueryHandler(catalogDb, inventoryEngine, new FakeFulfillmentRouter()),
            new AddCartItemCommandHandler(commerceDb, catalogDb, guestUser, inventoryEngine, new FakeFulfillmentRouter(), cartResponseBuilder, new FakeMembershipAccessProvider()),
            new GetCartQueryHandler(commerceDb, guestUser, cartResponseBuilder));

        var messagingOptions = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"messaging-{Guid.NewGuid():N}").Options;
        var messagingDb = new MessagingDbContext(messagingOptions);

        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var engine = CreateEngine(messagingDb, sender, provider);

        const string phone = "+201234567890";

        // Turn 1: first-ever contact — language prompt, regardless of what was typed.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        Assert.Contains("English", provider.SentMessages[^1].Message);

        // Turn 2: choose English -> Main Menu.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);
        Assert.Contains("Browse Games", provider.SentMessages[^1].Message);

        // Turn 3: Browse Games -> category list (real GetCategoryTreeQuery).
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);
        Assert.Contains("Category", provider.SentMessages[^1].Message);

        // Turn 4: pick the category -> product list (real GetProductsQuery), price included.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);
        Assert.Contains("Product", provider.SentMessages[^1].Message);
        Assert.Contains("19.99", provider.SentMessages[^1].Message);

        // Turn 5: pick the product -> product detail + variant options (real GetProductByIdQuery +
        // GetStorefrontProductConfigurationsQuery).
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);
        Assert.Contains(variant.Sku, provider.SentMessages[^1].Message);

        // Turn 6: pick the variant -> price + live availability (real IInventoryEngine-backed stock).
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);
        Assert.Contains("In stock", provider.SentMessages[^1].Message);
        Assert.Contains("Add to Cart", provider.SentMessages[^1].Message);

        // Turn 7: Add to Cart -> real AddCartItemCommand, cart deep link in the reply.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "2"), CancellationToken.None);
        Assert.Contains("Added to cart", provider.SentMessages[^1].Message);
        Assert.Contains("Browse Games", provider.SentMessages[^1].Message); // back at Main Menu

        Assert.Equal(7, provider.SentMessages.Count);

        var cart = await commerceDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.GuestSessionId == $"whatsapp:{phone}");

        Assert.NotNull(cart);
        var item = Assert.Single(cart!.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(variant.Id, item.ProductVariantId);
        Assert.Equal(1, item.Quantity);

        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.Main, session.CurrentMenu);
        Assert.False(session.IsLinked);
    }

    [Fact]
    public async Task ExpiredSession_ReturnsToMainMenu_WithoutLosingLanguage()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var guestUser = new FakeCurrentUserService(userId: null);
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), guestUser);

        var sender = new MultiHandlerFakeSender(
            new GetCategoryTreeQueryHandler(catalogDb),
            new GetProductsQueryHandler(catalogDb, guestUser, new FakeMembershipAccessProvider(), NullLogger<GetProductsQueryHandler>.Instance),
            new GetProductByIdQueryHandler(catalogDb, guestUser, new FakeMembershipAccessProvider()),
            new GetStorefrontProductConfigurationsQueryHandler(catalogDb, inventoryEngine, new FakeFulfillmentRouter()),
            new AddCartItemCommandHandler(commerceDb, catalogDb, guestUser, inventoryEngine, new FakeFulfillmentRouter(), cartResponseBuilder, new FakeMembershipAccessProvider()),
            new GetCartQueryHandler(commerceDb, guestUser, cartResponseBuilder));

        var messagingOptions = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"messaging-{Guid.NewGuid():N}").Options;
        var messagingDb = new MessagingDbContext(messagingOptions);
        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var engine = CreateEngine(messagingDb, sender, provider);

        const string phone = "+201234567891";

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "2"), CancellationToken.None); // Arabic

        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal("ar", session.LanguageCode);

        // Simulate the sliding window having lapsed since the last message.
        session.Touch(TimeSpan.FromMinutes(-1), DateTimeOffset.UtcNow.AddHours(-1));
        await messagingDb.SaveChangesAsync();

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "99"), CancellationToken.None);

        var reloaded = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.Main, reloaded.CurrentMenu);
        Assert.Equal("ar", reloaded.LanguageCode); // language survives the reset, only navigation resets
    }
}
