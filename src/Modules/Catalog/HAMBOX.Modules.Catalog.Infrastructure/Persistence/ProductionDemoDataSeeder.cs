using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Seeds a minimal storefront catalog when production starts with an empty database.
/// </summary>
internal sealed class ProductionDemoDataSeeder(
    CatalogDbContext dbContext,
    IHostEnvironment environment,
    ILogger<ProductionDemoDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        if (await dbContext.Products.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = new[]
        {
            Category.Create("مفاتيح الألعاب", "Game Keys", "game-keys"),
            Category.Create("بطاقات الهدايا", "Gift Cards", "gift-cards"),
            Category.Create("اشتراكات", "Subscriptions", "subscriptions"),
            Category.Create("شحن الحسابات", "Top Ups", "top-ups"),
        };

        dbContext.Categories.AddRange(categories);
        await dbContext.SaveChangesAsync(cancellationToken);

        var categoryBySlug = categories.ToDictionary(category => category.Slug);

        var products = new[]
        {
            CreateProduct("فيفا 26", "EA Sports FC 26", "Instant digital delivery for PC.", "توصيل رقمي فوري لجهاز الكمبيوتر.", 59.99m, categoryBySlug["game-keys"]),
            CreateProduct("كول أوف ديوتي", "Call of Duty: Black Ops 7", "Premium edition game key.", "مفتاح لعبة الإصدار المميز.", 69.99m, categoryBySlug["game-keys"]),
            CreateProduct("بطاقة بلايستيشن", "PlayStation Store Gift Card $50", "Redeem on the PlayStation Store.", "استخدمها في متجر بلايستيشن.", 49.99m, categoryBySlug["gift-cards"]),
            CreateProduct("بطاقة ستيم", "Steam Wallet $25", "Add funds to your Steam wallet.", "أضف رصيدًا إلى محفظة ستيم.", 24.99m, categoryBySlug["gift-cards"]),
            CreateProduct("نتفليكس", "Netflix Premium 1 Month", "One month premium subscription.", "اشتراك بريميوم لمدة شهر.", 19.99m, categoryBySlug["subscriptions"]),
            CreateProduct("سبوتيفاي", "Spotify Premium 3 Months", "Three months of ad-free music.", "ثلاثة أشهر من الموسيقى بدون إعلانات.", 29.99m, categoryBySlug["subscriptions"]),
            CreateProduct("شحن ببجي", "PUBG Mobile UC 1800", "Top up UC for PUBG Mobile.", "شحن UC للعبة ببجي موبايل.", 17.99m, categoryBySlug["top-ups"]),
            CreateProduct("شحن فري فاير", "Free Fire Diamonds 2200", "Diamonds for Free Fire.", "ألماس لفري فاير.", 21.99m, categoryBySlug["top-ups"]),
        };

        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {ProductCount} demo products across {CategoryCount} categories.", products.Length, categories.Length);
    }

    private static Product CreateProduct(
        string nameAr,
        string nameEn,
        string descriptionEn,
        string descriptionAr,
        decimal price,
        Category category)
    {
        var product = Product.Create(nameAr, nameEn, descriptionAr, descriptionEn, price, category.Id);
        product.Activate();
        return product;
    }
}
