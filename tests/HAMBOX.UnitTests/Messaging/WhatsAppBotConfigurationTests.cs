using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.GetWhatsAppBotConfiguration;
using HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.UpdateWhatsAppBotConfiguration;
using HAMBOX.Modules.Messaging.Application.Services;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using HAMBOX.Modules.Messaging.Domain.Conversations;
using HAMBOX.Modules.Messaging.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Infrastructure.Providers;
using HAMBOX.Modules.Messaging.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Messaging.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Messaging;

/// <summary>
/// The admin-configurable menu presentation feature — default fallback, per-item enable/reorder/relabel
/// reflected by the engine, validator guardrails on the fixed action set, and cache invalidation on
/// save. Does not re-test Catalog/Commerce/Support business logic (unchanged, already covered by
/// <see cref="WhatsAppBotEngineTests"/>/<see cref="WhatsAppBotEngineHardeningTests"/>) — only the new
/// configuration layer and that it never affects those flows.
/// </summary>
public sealed class WhatsAppBotConfigurationTests
{
    private static MessagingDbContext CreateMessagingDb() =>
        new(new DbContextOptionsBuilder<MessagingDbContext>().UseInMemoryDatabase($"messaging-{Guid.NewGuid():N}").Options);

    private static async Task<(WhatsAppBotEngine Engine, FakeWhatsAppProvider Provider, MessagingDbContext MessagingDb, WhatsAppBotConfigurationProvider ConfigProvider)>
        CreateEngineHarnessAsync()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (_, _, variant) = MessagingTestFixtures.SeedCatalog(catalogDb);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 5;
        var currentUser = new FakeCurrentUserService(userId: null);

        var cartResponseBuilder = new HAMBOX.Modules.Commerce.Application.Services.CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        var sender = new MultiHandlerFakeSender(
            new HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryTree.GetCategoryTreeQueryHandler(catalogDb),
            new HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts.GetProductsQueryHandler(
                catalogDb, currentUser, new FakeMembershipAccessProvider(), NullLogger<HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts.GetProductsQueryHandler>.Instance),
            new HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById.GetProductByIdQueryHandler(catalogDb, currentUser, new FakeMembershipAccessProvider()),
            new HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration.GetStorefrontProductConfigurationsQueryHandler(catalogDb, inventoryEngine, new FakeFulfillmentRouter()),
            new HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem.AddCartItemCommandHandler(
                commerceDb, catalogDb, currentUser, inventoryEngine, new FakeFulfillmentRouter(), cartResponseBuilder, new FakeMembershipAccessProvider()),
            new HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart.GetCartQueryHandler(commerceDb, currentUser, cartResponseBuilder));

        var messagingDb = CreateMessagingDb();
        var configProvider = MessagingTestFixtures.CreateConfigProvider(messagingDb);
        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var engine = MessagingTestFixtures.CreateEngine(messagingDb, sender, provider, configProvider);

        return (engine, provider, messagingDb, configProvider);
    }

    private static async Task<string> GetMainMenuTextAsync(WhatsAppBotEngine engine, FakeWhatsAppProvider provider, string phone, string language)
    {
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, language), CancellationToken.None);
        return provider.SentMessages[^1].Message;
    }

    // ── Default configuration / safe fallback ───────────────────────────────────────────────────────

    [Fact]
    public async Task Provider_WithNoSeededData_ReturnsSafeDefaults()
    {
        var messagingDb = CreateMessagingDb();
        var provider = MessagingTestFixtures.CreateConfigProvider(messagingDb);

        var snapshot = await provider.GetAsync(CancellationToken.None);

        Assert.Equal(7, snapshot.EnabledItemsInOrder.Count);
        Assert.Equal(WhatsAppBotConfigurationDefaults.WelcomeMessageEn, snapshot.WelcomeMessageEn);
        Assert.Equal(WhatsAppBotConfigurationDefaults.FallbackMessageEn, snapshot.FallbackMessageEn);
    }

    [Fact]
    public async Task Engine_WithNoSeededConfiguration_StillRendersMainMenu()
    {
        var (engine, provider, _, _) = await CreateEngineHarnessAsync();
        var reply = await GetMainMenuTextAsync(engine, provider, "+201100000001", "1");

        Assert.Contains("Browse Games", reply);
    }

    // ── Disable / enable / reorder / labels ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Engine_DisabledMenuItem_IsHiddenFromMainMenu()
    {
        var (engine, provider, messagingDb, configProvider) = await CreateEngineHarnessAsync();
        await SeedConfigAsync(messagingDb, items =>
        {
            var cart = items.Single(i => i.Action == WhatsAppMenuAction.Cart);
            cart.SetEnabled(false);
        });
        configProvider.Invalidate();

        var reply = await GetMainMenuTextAsync(engine, provider, "+201100000002", "1");

        Assert.DoesNotContain("My Cart", reply);
        Assert.Contains("Browse Games", reply); // the rest of the menu still renders
    }

    [Fact]
    public async Task Engine_ReorderedMenu_NumbersMatchNewOrder()
    {
        var (engine, provider, messagingDb, configProvider) = await CreateEngineHarnessAsync();

        // Move Cart to the first position.
        await SeedConfigAsync(messagingDb, items =>
        {
            var ordered = items.OrderBy(i => i.SortOrder).ToList();
            var cart = ordered.Single(i => i.Action == WhatsAppMenuAction.Cart);
            ordered.Remove(cart);
            ordered.Insert(0, cart);
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].SetSortOrder(i);
            }
        });
        configProvider.Invalidate();

        const string phone = "+201100000003";
        var mainMenu = await GetMainMenuTextAsync(engine, provider, phone, "1");
        Assert.StartsWith(WhatsAppBotConfigurationDefaults.WelcomeMessageEn + "\n1. My Cart", mainMenu);

        // Selecting the new "1" must reach Cart (empty cart message), not Browse Categories.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);
        Assert.Contains("cart", provider.SentMessages[^1].Message, StringComparison.OrdinalIgnoreCase);

        var session = await messagingDb.WhatsAppConversationSessions.SingleAsync(s => s.PhoneNumber == phone);
        Assert.Equal(WhatsAppMenuState.Cart, session.CurrentMenu);
    }

    [Fact]
    public async Task Engine_UsesEnglishLabel_ForEnglishSession()
    {
        var (engine, provider, messagingDb, configProvider) = await CreateEngineHarnessAsync();
        await SeedConfigAsync(messagingDb, items =>
            items.Single(i => i.Action == WhatsAppMenuAction.Cart).SetLabels("Shopping Bag", "حقيبة التسوق"));
        configProvider.Invalidate();

        var reply = await GetMainMenuTextAsync(engine, provider, "+201100000004", "1");
        Assert.Contains("Shopping Bag", reply);
    }

    [Fact]
    public async Task Engine_UsesArabicLabel_ForArabicSession()
    {
        var (engine, provider, messagingDb, configProvider) = await CreateEngineHarnessAsync();
        await SeedConfigAsync(messagingDb, items =>
            items.Single(i => i.Action == WhatsAppMenuAction.Cart).SetLabels("Shopping Bag", "حقيبة التسوق"));
        configProvider.Invalidate();

        var reply = await GetMainMenuTextAsync(engine, provider, "+201100000005", "2"); // Arabic
        Assert.Contains("حقيبة التسوق", reply);
    }

    [Fact]
    public async Task Engine_WelcomeMessage_IsConfigurable()
    {
        var (engine, provider, messagingDb, configProvider) = await CreateEngineHarnessAsync();
        await SeedConfigAsync(messagingDb, config: c => c.SetWelcomeMessage("Hey there, gamer!", "أهلاً أيها اللاعب!"));
        configProvider.Invalidate();

        var reply = await GetMainMenuTextAsync(engine, provider, "+201100000006", "1");
        Assert.Contains("Hey there, gamer!", reply);
    }

    [Fact]
    public async Task Engine_FallbackMessage_IsConfigurableAndShownOnInvalidInput()
    {
        var (engine, provider, messagingDb, configProvider) = await CreateEngineHarnessAsync();
        await SeedConfigAsync(messagingDb, config: c => c.SetFallbackMessage("Try a number please!", "جرب رقمًا من فضلك!"));
        configProvider.Invalidate();

        const string phone = "+201100000007";
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "hi"), CancellationToken.None);
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "1"), CancellationToken.None);

        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "not-a-number"), CancellationToken.None);
        Assert.Contains("Try a number please!", provider.SentMessages[^1].Message);
    }

    // ── Validator guardrails ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validator_MissingAction_IsRejected()
    {
        var validator = new UpdateWhatsAppBotConfigurationCommandValidator();
        var items = AllEnabledDefaultItems().Where(i => i.Action != WhatsAppMenuAction.Language).ToList(); // 6 of 7

        var result = validator.Validate(new UpdateWhatsAppBotConfigurationCommand(
            "Welcome", "أهلاً", "Sorry", "عذرًا", items));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateWhatsAppBotConfigurationCommand.Items));
    }

    [Fact]
    public void Validator_DuplicateAction_IsRejected()
    {
        var validator = new UpdateWhatsAppBotConfigurationCommandValidator();
        var items = AllEnabledDefaultItems().ToList();
        items[6] = items[0]; // duplicate Action, still 7 entries but only 6 distinct actions

        var result = validator.Validate(new UpdateWhatsAppBotConfigurationCommand(
            "Welcome", "أهلاً", "Sorry", "عذرًا", items));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateWhatsAppBotConfigurationCommand.Items));
    }

    [Fact]
    public void Validator_AllItemsDisabled_IsRejected()
    {
        var validator = new UpdateWhatsAppBotConfigurationCommandValidator();
        var items = AllEnabledDefaultItems().Select(i => i with { IsEnabled = false }).ToList();

        var result = validator.Validate(new UpdateWhatsAppBotConfigurationCommand(
            "Welcome", "أهلاً", "Sorry", "عذرًا", items));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateWhatsAppBotConfigurationCommand.Items));
    }

    [Fact]
    public void Validator_EmptyLabel_IsRejected()
    {
        var validator = new UpdateWhatsAppBotConfigurationCommandValidator();
        var items = AllEnabledDefaultItems().ToList();
        items[0] = items[0] with { LabelEn = "" };

        var result = validator.Validate(new UpdateWhatsAppBotConfigurationCommand(
            "Welcome", "أهلاً", "Sorry", "عذرًا", items));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Items[0].LabelEn");
    }

    [Fact]
    public void Validator_ValidFullPayload_PassesWithNoErrors()
    {
        var validator = new UpdateWhatsAppBotConfigurationCommandValidator();
        var result = validator.Validate(new UpdateWhatsAppBotConfigurationCommand(
            "Welcome", "أهلاً", "Sorry", "عذرًا", AllEnabledDefaultItems().ToList()));

        Assert.True(result.IsValid);
    }

    // ── Handler: update → cache invalidation → next read/message reflects it, no restart ──────────

    [Fact]
    public async Task Handler_Update_InvalidatesCache_NextEngineMessageUsesNewConfig()
    {
        var (engine, provider, messagingDb, configProvider) = await CreateEngineHarnessAsync();
        var currentUser = new FakeCurrentUserService(userId: "admin-1");
        var handler = new UpdateWhatsAppBotConfigurationCommandHandler(messagingDb, currentUser, configProvider);

        const string phone = "+201100000008";
        var before = await GetMainMenuTextAsync(engine, provider, phone, "1");
        Assert.Contains("My Cart", before);

        var items = AllEnabledDefaultItems()
            .Select(i => i.Action == WhatsAppMenuAction.Cart ? i with { LabelEn = "Basket" } : i)
            .ToList();
        var result = await handler.Handle(
            new UpdateWhatsAppBotConfigurationCommand("Welcome", "أهلاً", "Sorry", "عذرًا", items), CancellationToken.None);
        Assert.True(result.IsSuccess);

        // Same engine/provider instances as before — proves the cache was invalidated, not that a
        // fresh process/instance happened to read the DB.
        await engine.HandleInboundMessageAsync(new WhatsAppInboundMessage(phone, "0"), CancellationToken.None);
        Assert.Contains("Basket", provider.SentMessages[^1].Message);
        Assert.DoesNotContain("My Cart", provider.SentMessages[^1].Message);
    }

    [Fact]
    public async Task Handler_Update_WritesAuditRowsForMeaningfulChanges()
    {
        var messagingDb = CreateMessagingDb();
        await SeedConfigAsync(messagingDb); // realistic starting point: the seeder already ran
        var configProvider = MessagingTestFixtures.CreateConfigProvider(messagingDb);
        var currentUser = new FakeCurrentUserService(userId: "admin-2");
        var handler = new UpdateWhatsAppBotConfigurationCommandHandler(messagingDb, currentUser, configProvider);

        var items = AllEnabledDefaultItems()
            .Select(i => i.Action == WhatsAppMenuAction.Alerts ? i with { IsEnabled = false } : i)
            .ToList();

        await handler.Handle(
            new UpdateWhatsAppBotConfigurationCommand("New welcome", "ترحيب جديد", "Sorry", "عذرًا", items), CancellationToken.None);

        var logs = await messagingDb.WhatsAppBotConfigAuditLogs.ToListAsync();
        Assert.Contains(logs, l => l.Action == WhatsAppBotConfigAuditAction.ItemDisabled && l.Target == "Alerts");
        Assert.Contains(logs, l => l.Action == WhatsAppBotConfigAuditAction.WelcomeMessageChanged);
        Assert.All(logs, l => Assert.Equal("admin-2", l.ActorUserId));
    }

    [Fact]
    public async Task Handler_Update_MaxLengthWelcomeAndFallbackMessages_DoesNotOverflowAuditColumn()
    {
        // WelcomeMessageEn/Ar and FallbackMessageEn/Ar are each validated up to 500 chars. The audit
        // handler composes them as "en / ar" (up to 1003 chars) into WhatsAppBotConfigAuditLog's
        // OldValue/NewValue, whose column is only 1000 chars wide — an otherwise perfectly valid save
        // must not throw a truncation DbUpdateException from SaveChangesAsync.
        var messagingDb = CreateMessagingDb();
        await SeedConfigAsync(messagingDb); // starting values are short, so both messages register as "changed"
        var configProvider = MessagingTestFixtures.CreateConfigProvider(messagingDb);
        var currentUser = new FakeCurrentUserService(userId: "admin-3");
        var handler = new UpdateWhatsAppBotConfigurationCommandHandler(messagingDb, currentUser, configProvider);

        var maxWelcomeEn = new string('W', 500);
        var maxWelcomeAr = new string('و', 500);
        var maxFallbackEn = new string('F', 500);
        var maxFallbackAr = new string('ف', 500);

        var result = await handler.Handle(
            new UpdateWhatsAppBotConfigurationCommand(maxWelcomeEn, maxWelcomeAr, maxFallbackEn, maxFallbackAr, AllEnabledDefaultItems().ToList()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var logs = await messagingDb.WhatsAppBotConfigAuditLogs.ToListAsync();
        Assert.Contains(logs, l => l.Action == WhatsAppBotConfigAuditAction.WelcomeMessageChanged);
        Assert.Contains(logs, l => l.Action == WhatsAppBotConfigAuditAction.FallbackMessageChanged);
        Assert.All(logs, l => Assert.True((l.OldValue?.Length ?? 0) <= 1000 && (l.NewValue?.Length ?? 0) <= 1000));
    }

    [Fact]
    public async Task GetQuery_ReturnsSavedConfiguration()
    {
        var messagingDb = CreateMessagingDb();
        var configProvider = MessagingTestFixtures.CreateConfigProvider(messagingDb);
        var currentUser = new FakeCurrentUserService(userId: "admin-3");
        var updateHandler = new UpdateWhatsAppBotConfigurationCommandHandler(messagingDb, currentUser, configProvider);
        var getHandler = new GetWhatsAppBotConfigurationQueryHandler(messagingDb);

        await updateHandler.Handle(
            new UpdateWhatsAppBotConfigurationCommand("Hi!", "أهلاً!", "Huh?", "ماذا؟", AllEnabledDefaultItems().ToList()),
            CancellationToken.None);

        var result = await getHandler.Handle(new GetWhatsAppBotConfigurationQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hi!", result.Value.WelcomeMessageEn);
        Assert.Equal(7, result.Value.Items.Count);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<WhatsAppMenuItemUpdate> AllEnabledDefaultItems() =>
        WhatsAppBotConfigurationDefaults.Items
            .Select(i => new WhatsAppMenuItemUpdate(i.Action, true, i.LabelEn, i.LabelAr))
            .ToList();

    private static async Task SeedConfigAsync(
        MessagingDbContext db, Action<List<WhatsAppMenuItem>>? items = null, Action<WhatsAppBotConfiguration>? config = null)
    {
        var existingConfig = await db.WhatsAppBotConfigurations.FirstOrDefaultAsync();
        if (existingConfig is null)
        {
            existingConfig = WhatsAppBotConfiguration.CreateDefault(
                WhatsAppBotConfigurationDefaults.WelcomeMessageEn, WhatsAppBotConfigurationDefaults.WelcomeMessageAr,
                WhatsAppBotConfigurationDefaults.FallbackMessageEn, WhatsAppBotConfigurationDefaults.FallbackMessageAr);
            db.WhatsAppBotConfigurations.Add(existingConfig);
        }
        config?.Invoke(existingConfig);

        var existingItems = await db.WhatsAppMenuItems.ToListAsync();
        if (existingItems.Count == 0)
        {
            var order = 0;
            foreach (var (action, labelEn, labelAr) in WhatsAppBotConfigurationDefaults.Items)
            {
                var item = WhatsAppMenuItem.CreateDefault(action, order, labelEn, labelAr);
                db.WhatsAppMenuItems.Add(item);
                existingItems.Add(item);
                order++;
            }
        }
        items?.Invoke(existingItems);

        await db.SaveChangesAsync();
    }
}
