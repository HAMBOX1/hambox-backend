using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Application.Services;
using HAMBOX.Modules.Messaging.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Infrastructure.Providers;
using HAMBOX.Modules.Messaging.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Caching.Memory;

namespace HAMBOX.UnitTests.Messaging.TestDoubles;

/// <summary>Shared setup for WhatsApp bot engine tests — one active category/product/variant, and an
/// engine wired against real Catalog/Commerce handlers via <see cref="MultiHandlerFakeSender"/>.</summary>
internal static class MessagingTestFixtures
{
    public static (Category Category, Product Product, ProductVariant Variant) SeedCatalog(CatalogDbContext catalogDb)
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        product.Activate();

        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        catalogDb.SaveChanges();

        return (category, product, variant);
    }

    public static WhatsAppBotEngine CreateEngine(
        MessagingDbContext messagingDb,
        MultiHandlerFakeSender sender,
        FakeWhatsAppProvider provider) =>
        CreateEngine(messagingDb, sender, provider, CreateConfigProvider(messagingDb));

    public static WhatsAppBotEngine CreateEngine(
        MessagingDbContext messagingDb,
        MultiHandlerFakeSender sender,
        FakeWhatsAppProvider provider,
        WhatsAppBotConfigurationProvider menuConfigProvider)
    {
        var linkVerification = new WhatsAppLinkVerificationService(
            new UnusedIdentityDbContext(), messagingDb, new UnusedOtpCodeGenerator(), new FakeCommunicationService());

        return new WhatsAppBotEngine(
            messagingDb, sender, provider, linkVerification,
            new NoOpWhatsAppUserContextScope(), new FakeMessagingPlatformSettingsProvider(), menuConfigProvider);
    }

    /// <summary>Real cache-aside provider (real <see cref="MemoryCache"/>, no mocking) so tests exercise
    /// the exact same caching/invalidation behavior production uses.</summary>
    public static WhatsAppBotConfigurationProvider CreateConfigProvider(MessagingDbContext messagingDb) =>
        new(messagingDb, new MemoryCache(new MemoryCacheOptions()));
}
