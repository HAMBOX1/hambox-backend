using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Seeds a storefront catalog when a development or production database starts empty.
/// </summary>
internal sealed class ProductionDemoDataSeeder(
    CatalogDbContext dbContext,
    IHostEnvironment environment,
    ILogger<ProductionDemoDataSeeder> logger)
{
    private sealed record DemoCategory(string NameAr, string NameEn, string Slug);

    private sealed record DemoProduct(
        string NameAr,
        string NameEn,
        string DescriptionAr,
        string DescriptionEn,
        decimal Price,
        string CategorySlug,
        string AccentColor);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment() && !environment.IsProduction())
        {
            return;
        }

        var categories = await EnsureCategoriesAsync(cancellationToken);

        if (await dbContext.Products.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        var products = DemoProducts
            .Select((demo, index) => CreateProduct(demo, categories[demo.CategorySlug], index))
            .ToArray();

        dbContext.Products.AddRange(products);

        foreach (var product in products)
        {
            var regionGroup = ProductOptionGroup.Create(product.Id, "region", "Region", 0, true);
            var optionGlobal = regionGroup.AddOption("global", "Global", 0);
            var optionUS = regionGroup.AddOption("us", "United States", 1);
            
            dbContext.ProductOptionGroups.Add(regionGroup);
            dbContext.ProductOptions.AddRange(regionGroup.Options);

            var variantGlobal = ProductVariant.Create(product.Id, $"SKU-{product.Id.ToString()[..6]}-GLB", priceOverride: product.Price);
            variantGlobal.SetOptions([optionGlobal.Id]);
            variantGlobal.Activate();
            
            dbContext.ProductVariants.Add(variantGlobal);
            dbContext.ProductVariantOptions.AddRange(variantGlobal.SelectedOptions);

            var batchGlobal = InventoryBatch.Create(variantGlobal.Id, "Initial Batch Global", purchaseCost: product.Price * 0.5m);
            dbContext.InventoryBatches.Add(batchGlobal);

            for (int i = 0; i < 5; i++)
            {
                var code = DigitalInventoryCode.Create(variantGlobal.Id, batchGlobal.Id, $"{variantGlobal.Sku}-CODE-{i + 1}");
                dbContext.DigitalInventoryCodes.Add(code);
                batchGlobal.RecordImport(1);
            }

            var variantUS = ProductVariant.Create(product.Id, $"SKU-{product.Id.ToString()[..6]}-US", priceOverride: product.Price * 0.9m);
            variantUS.SetOptions([optionUS.Id]);
            variantUS.Activate();
            
            dbContext.ProductVariants.Add(variantUS);
            dbContext.ProductVariantOptions.AddRange(variantUS.SelectedOptions);

            var batchUS = InventoryBatch.Create(variantUS.Id, "Initial Batch US", purchaseCost: product.Price * 0.45m);
            dbContext.InventoryBatches.Add(batchUS);

            for (int i = 0; i < 3; i++)
            {
                var code = DigitalInventoryCode.Create(variantUS.Id, batchUS.Id, $"{variantUS.Sku}-CODE-{i + 1}");
                dbContext.DigitalInventoryCodes.Add(code);
                batchUS.RecordImport(1);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {ProductCount} demo products across {CategoryCount} categories with options, variants, and inventory codes.",
            products.Length,
            categories.Count);
    }

    private async Task<Dictionary<string, Category>> EnsureCategoriesAsync(CancellationToken cancellationToken)
    {
        var slugs = DemoCategories.Select(category => category.Slug).ToArray();
        var categories = await dbContext.Categories
            .Where(category => slugs.Contains(category.Slug))
            .ToDictionaryAsync(category => category.Slug, cancellationToken);

        foreach (var demo in DemoCategories)
        {
            if (categories.ContainsKey(demo.Slug))
            {
                continue;
            }

            var category = Category.Create(demo.NameAr, demo.NameEn, demo.Slug);
            dbContext.Categories.Add(category);
            categories[demo.Slug] = category;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return categories;
    }

    private static Product CreateProduct(DemoProduct demo, Category category, int index)
    {
        var product = Product.Create(
            demo.NameAr,
            demo.NameEn,
            demo.DescriptionAr,
            demo.DescriptionEn,
            demo.Price,
            category.Id);

        product.AddImage(
            CreateImageDataUri(demo.NameEn, demo.AccentColor),
            $"seed/catalog/{index + 1:D2}.svg",
            $"{Slugify(demo.NameEn)}.svg",
            "image/svg+xml",
            1024,
            0,
            true);

        product.Activate();
        return product;
    }

    private static string CreateImageDataUri(string title, string accentColor)
    {
        var encodedTitle = Uri.EscapeDataString(title);
        var encodedColor = Uri.EscapeDataString(accentColor);

        return "data:image/svg+xml,"
            + "%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 640 420'%3E"
            + "%3Cdefs%3E%3ClinearGradient id='g' x1='0' y1='0' x2='1' y2='1'%3E"
            + $"%3Cstop offset='0' stop-color='{encodedColor}'/%3E"
            + "%3Cstop offset='1' stop-color='%2310151f'/%3E"
            + "%3C/linearGradient%3E%3C/defs%3E"
            + "%3Crect width='640' height='420' rx='36' fill='url(%23g)'/%3E"
            + "%3Ccircle cx='525' cy='88' r='76' fill='white' fill-opacity='.16'/%3E"
            + "%3Crect x='64' y='64' width='512' height='292' rx='28' fill='black' fill-opacity='.2'/%3E"
            + $"%3Ctext x='88' y='218' fill='white' font-family='Arial,sans-serif' font-size='44' font-weight='700'%3E{encodedTitle}%3C/text%3E"
            + "%3Ctext x='88' y='272' fill='white' fill-opacity='.78' font-family='Arial,sans-serif' font-size='22'%3EHAMBOX DIGITAL CODE%3C/text%3E"
            + "%3C/svg%3E";
    }

    private static string Slugify(string value) =>
        value.ToLowerInvariant()
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("+", "plus", StringComparison.Ordinal)
            .Replace(" ", "-", StringComparison.Ordinal);

    private static readonly DemoCategory[] DemoCategories =
    [
        new("Game Keys", "Game Keys", "game-keys"),
        new("Gift Cards", "Gift Cards", "gift-cards"),
        new("Subscriptions", "Subscriptions", "subscriptions"),
        new("Top Ups", "Top Ups", "top-ups"),
        new("Software", "Software", "software"),
        new("Memberships", "Memberships", "memberships"),
    ];

    private static readonly DemoProduct[] DemoProducts =
    [
        new("EA Sports FC 26", "EA Sports FC 26", "Instant PC game key with global redemption.", "Instant PC game key with global redemption.", 59.99m, "game-keys", "#20b486"),
        new("Cyberpunk 2077 Ultimate", "Cyberpunk 2077 Ultimate", "Base game plus expansion delivered as a digital code.", "Base game plus expansion delivered as a digital code.", 44.99m, "game-keys", "#f6b73c"),
        new("Call of Duty: Black Ops 7", "Call of Duty: Black Ops 7", "Premium edition digital key with instant delivery.", "Premium edition digital key with instant delivery.", 69.99m, "game-keys", "#ef4444"),
        new("Minecraft Java & Bedrock", "Minecraft Java & Bedrock", "Redeemable code for Java and Bedrock editions.", "Redeemable code for Java and Bedrock editions.", 29.99m, "game-keys", "#22c55e"),
        new("PlayStation Store $50", "PlayStation Store $50", "Wallet top-up card for PlayStation Store.", "Wallet top-up card for PlayStation Store.", 49.99m, "gift-cards", "#2563eb"),
        new("Steam Wallet $25", "Steam Wallet $25", "Add funds to your Steam account instantly.", "Add funds to your Steam account instantly.", 24.99m, "gift-cards", "#0f766e"),
        new("Xbox Gift Card $30", "Xbox Gift Card $30", "Digital Xbox card for games, add-ons, and apps.", "Digital Xbox card for games, add-ons, and apps.", 29.99m, "gift-cards", "#16a34a"),
        new("Nintendo eShop $20", "Nintendo eShop $20", "Redeem on Nintendo eShop for games and DLC.", "Redeem on Nintendo eShop for games and DLC.", 19.99m, "gift-cards", "#dc2626"),
        new("Netflix Premium 1 Month", "Netflix Premium 1 Month", "One month premium subscription code.", "One month premium subscription code.", 19.99m, "subscriptions", "#e11d48"),
        new("Spotify Premium 3 Months", "Spotify Premium 3 Months", "Three months of ad-free music streaming.", "Three months of ad-free music streaming.", 29.99m, "subscriptions", "#10b981"),
        new("Discord Nitro 1 Month", "Discord Nitro 1 Month", "Nitro membership with boosts and profile perks.", "Nitro membership with boosts and profile perks.", 9.99m, "subscriptions", "#6366f1"),
        new("YouTube Premium 1 Month", "YouTube Premium 1 Month", "Ad-free video and music subscription code.", "Ad-free video and music subscription code.", 13.99m, "subscriptions", "#f43f5e"),
        new("PUBG Mobile UC 1800", "PUBG Mobile UC 1800", "UC top-up for PUBG Mobile accounts.", "UC top-up for PUBG Mobile accounts.", 17.99m, "top-ups", "#f97316"),
        new("Free Fire Diamonds 2200", "Free Fire Diamonds 2200", "Diamond top-up for Free Fire.", "Diamond top-up for Free Fire.", 21.99m, "top-ups", "#fb7185"),
        new("Microsoft 365 Personal", "Microsoft 365 Personal", "One year personal productivity subscription.", "One year personal productivity subscription.", 54.99m, "software", "#0ea5e9"),
        new("Windows 11 Pro Key", "Windows 11 Pro Key", "Activation key for Windows 11 Pro.", "Activation key for Windows 11 Pro.", 39.99m, "software", "#38bdf8"),
    ];
}
