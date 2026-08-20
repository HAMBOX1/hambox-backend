using HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryTree;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;
using HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Domain.Conversations;
using HAMBOX.Modules.Messaging.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Infrastructure.Providers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Messaging.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Messaging;

/// <summary>
/// Focused hardening tests: the security boundaries and invalid-input/back-navigation edge cases found
/// during the audit, not general coverage. Each test here corresponds to a specific finding — see the
/// hardening pass report for which bug each one would have caught before the fix.
/// </summary>
public sealed class WhatsAppBotEngineHardeningTests
{
    private static MultiHandlerFakeSender CreateSender(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        FakeInventoryEngine inventoryEngine,
        FakeCurrentUserService currentUser)
    {
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);

        return new MultiHandlerFakeSender(
            new GetCategoryTreeQueryHandler(catalogDb),
            new GetProductsQueryHandler(catalogDb, currentUser, new FakeMembershipAccessProvider(), NullLogger<GetProductsQueryHandler>.Instance),
            new GetProductByIdQueryHandler(catalogDb, currentUser, new FakeMembershipAccessProvider()),
            new GetStorefrontProductConfigurationsQueryHandler(catalogDb, inventoryEngine, new FakeFulfillmentRouter()),
            new AddCartItemCommandHandler(commerceDb, catalogDb, currentUser, inventoryEngine, new FakeFulfillmentRouter(), cartResponseBuilder, new FakeMembershipAccessProvider()),
            new GetCartQueryHandler(commerceDb, currentUser, cartResponseBuilder));
    }

    private static (MessagingDbContext MessagingDb, FakeWhatsAppProvider Provider, HAMBOX.Modules.Messaging.Application.Services.WhatsAppBotEngine Engine)
        CreateHarness()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var currentUser = new FakeCurrentUserService(userId: null);
        var sender = CreateSender(commerceDb, catalogDb, inventoryEngine, currentUser);

        var messagingDb = new MessagingDbContext(new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"messaging-{Guid.NewGuid():N}").Options);
        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var engine = MessagingTestFixtures.CreateEngine(messagingDb, sender, provider);

        return (messagingDb, provider, engine);
    }

    // ── Security: unlinked/expired sessions must never reach Orders/Alerts/Support data ────────────

    [Fact]
    public async Task UnlinkedUser_SelectingOrders_IsPromptedToLink_NeverQueriesOrders()
    {
        // CreateSender only wires Catalog/Cart handlers — GetOrdersQuery is not one of them, so if the
        // gate ever fails and the engine tries to fetch orders anyway, MultiHandlerFakeSender throws
        // NotSupportedException and this test fails loudly instead of silently leaking data.
        var (messagingDb, provider, engine) = CreateHarness();
        const string phone = "+201000000001";

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // English

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "4"), CancellationToken.None); // My Orders

        var reply = provider.SentMessages[^1].Message;
        Assert.Contains("email", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("order", reply, StringComparison.OrdinalIgnoreCase);

        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.LinkEmailPrompt, session.CurrentMenu);
        Assert.False(session.IsLinked);
    }

    [Fact]
    public async Task ExpiredSession_WithPriorLink_MustReVerifyBeforeOrders()
    {
        var (messagingDb, provider, engine) = CreateHarness();
        const string phone = "+201000000002";

        // Simulate a session that was linked in the past (bypassing the email/code flow directly, since
        // only the *consequence* of an existing link is under test here) and has since gone idle past
        // the sliding window.
        var session = WhatsAppConversationSession.CreateNew(phone, TimeSpan.FromMinutes(30));
        session.LinkToCustomer(Guid.NewGuid().ToString());
        session.Touch(TimeSpan.FromMinutes(-1), DateTimeOffset.UtcNow.AddHours(-1)); // forces expiry
        messagingDb.WhatsAppConversationSessions.Add(session);
        await messagingDb.SaveChangesAsync();

        Assert.True(session.IsLinked); // sanity: the precondition this test is exercising

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "4"), CancellationToken.None); // My Orders

        var reply = provider.SentMessages[^1].Message;
        Assert.Contains("email", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("order", reply, StringComparison.OrdinalIgnoreCase);

        var reloaded = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.False(reloaded.IsLinked); // the expired link must be gone, not just bypassed for this turn
        Assert.Equal(WhatsAppMenuState.LinkEmailPrompt, reloaded.CurrentMenu);
    }

    // ── Pagination edge case ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pagination_PastLastPage_ShowsNoMoreResults_AndKeepsListingUsable()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (category, product, variant) = MessagingTestFixtures.SeedCatalog(catalogDb);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 5;
        var currentUser = new FakeCurrentUserService(userId: null);
        var sender = CreateSender(commerceDb, catalogDb, inventoryEngine, currentUser);

        var messagingDb = new MessagingDbContext(new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"messaging-{Guid.NewGuid():N}").Options);
        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var engine = MessagingTestFixtures.CreateEngine(messagingDb, sender, provider);

        const string phone = "+201000000003";
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // English
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // Browse
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // pick the only category

        // Only one product exists (page size 8) — there is no next page, but a user can still send "9".
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "9"), CancellationToken.None);

        var reply = provider.SentMessages[^1].Message;
        Assert.Contains("No more results", reply);
        Assert.Contains("Product", reply); // still shows the last known-good page, not an empty list

        // Selecting "1" must still work — proving context wasn't left pointing at an empty page.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);
        Assert.Contains(variant.Sku, provider.SentMessages[^1].Message);
    }

    // ── Resilience: corrupt/oversized stored state must fail safely, never crash the handler ────────

    [Fact]
    public async Task CorruptContextJson_NonGuidEntryInList_FailsSafely_DoesNotCrashOrDropTheReply()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (category, _, _) = MessagingTestFixtures.SeedCatalog(catalogDb);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var currentUser = new FakeCurrentUserService(userId: null);
        var sender = CreateSender(commerceDb, catalogDb, inventoryEngine, currentUser);

        var messagingDb = new MessagingDbContext(new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"messaging-{Guid.NewGuid():N}").Options);
        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var engine = MessagingTestFixtures.CreateEngine(messagingDb, sender, provider);

        const string phone = "+201000000007";
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // English
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // Browse -> categories listed

        // Simulate a corrupted row (manual edit, stale format from an old app version, etc.) — the
        // stored list of "ids" no longer contains valid GUIDs.
        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        session.SetContext("""{"ids":["not-a-guid"],"page":1}""");
        await messagingDb.SaveChangesAsync();

        // Before the fix, Guid.Parse threw FormatException here, which the webhook's outer catch would
        // swallow — SaveChangesAsync never ran, the session stayed corrupted forever, and the user got
        // no reply at all. Selecting the (now-invalid) first item must instead degrade gracefully.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);

        var reply = provider.SentMessages[^1].Message;
        Assert.Contains("pick a number", reply, StringComparison.OrdinalIgnoreCase);

        // And the session recovered — it's usable again, not stuck.
        session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.BrowseCategories, session.CurrentMenu);
    }

    [Fact]
    public async Task SearchTerm_LongerThanColumnBudget_IsTruncated_AndNeverThrowsOnSave()
    {
        var (messagingDb, provider, engine) = CreateHarness();
        const string phone = "+201000000008";

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // English
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "2"), CancellationToken.None); // Search

        // A real WhatsApp text message can be up to 4096 chars — well beyond ContextJson's 2000-char
        // column once wrapped in the MenuContext JSON shape. Before the fix this threw a truncation
        // DbUpdateException out of SaveChangesAsync with no reply ever sent.
        var longTerm = new string('a', 4000);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, longTerm), CancellationToken.None);

        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.True((session.ContextJson?.Length ?? 0) < 2000);
        Assert.NotEmpty(provider.SentMessages); // the search reply was actually sent
    }

    // ── Invalid input / back navigation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Main_InvalidInput_ShowsHint_AndStaysAtMain()
    {
        var (messagingDb, provider, engine) = CreateHarness();
        const string phone = "+201000000004";

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // English

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "banana"), CancellationToken.None);

        Assert.Contains("didn't understand", provider.SentMessages[^1].Message, StringComparison.OrdinalIgnoreCase);

        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.Main, session.CurrentMenu);
    }

    [Fact]
    public async Task LanguageSelect_CancelWithZero_KeepsLanguage_ReturnsToMain()
    {
        var (messagingDb, provider, engine) = CreateHarness();
        const string phone = "+201000000005";

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // English
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "7"), CancellationToken.None); // change language

        Assert.Contains("0. Cancel", provider.SentMessages[^1].Message);

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "0"), CancellationToken.None);

        Assert.Contains("Browse Games", provider.SentMessages[^1].Message); // back at the English main menu
        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal("en", session.LanguageCode);
        Assert.Equal(WhatsAppMenuState.Main, session.CurrentMenu);
    }

    [Fact]
    public async Task BackNavigation_WalksAllTheWayFromVariantSelect_ToMain()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (category, product, variant) = MessagingTestFixtures.SeedCatalog(catalogDb);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 5;
        var currentUser = new FakeCurrentUserService(userId: null);
        var sender = CreateSender(commerceDb, catalogDb, inventoryEngine, currentUser);

        var messagingDb = new MessagingDbContext(new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"messaging-{Guid.NewGuid():N}").Options);
        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var engine = MessagingTestFixtures.CreateEngine(messagingDb, sender, provider);

        const string phone = "+201000000006";
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // English
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // Browse
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // category
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // product
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None); // variant

        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.VariantSelect, session.CurrentMenu);

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "0"), CancellationToken.None); // back
        session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.ProductDetail, session.CurrentMenu);

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "0"), CancellationToken.None); // back
        session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.BrowseProducts, session.CurrentMenu);

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "0"), CancellationToken.None); // back
        session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.BrowseCategories, session.CurrentMenu);

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "0"), CancellationToken.None); // back
        session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.Main, session.CurrentMenu);
    }
}
