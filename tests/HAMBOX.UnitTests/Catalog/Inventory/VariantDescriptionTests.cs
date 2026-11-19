using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory;

/// <summary>The note a shopper sees on the product page while one exact variant is selected.</summary>
public sealed class VariantDescriptionTests
{
    [Fact]
    public async Task SavesASanitizedNote_AndTheInlineChoice()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), "PSN-ESSENTIAL-1M");
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        var result = await new SetVariantDescriptionCommandHandler(db).Handle(
            new SetVariantDescriptionCommand(variant.Id, "<p>Delivered in 5 minutes</p><script>alert(1)</script><img src=\"/uploads/a.png\">", true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await db.ProductVariants.SingleAsync(v => v.Id == variant.Id);
        Assert.Contains("Delivered in 5 minutes", saved.DescriptionHtml);
        Assert.Contains("/uploads/a.png", saved.DescriptionHtml);
        Assert.DoesNotContain("script", saved.DescriptionHtml, StringComparison.OrdinalIgnoreCase);
        Assert.True(saved.ShowDescriptionInline);
    }

    [Fact]
    public async Task AnEmptyNoteClearsIt()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), "PSN-ESSENTIAL-1M");
        variant.SetDescription("<p>Old</p>", true);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        await new SetVariantDescriptionCommandHandler(db).Handle(new SetVariantDescriptionCommand(variant.Id, "<p></p>", true), CancellationToken.None);

        var saved = await db.ProductVariants.SingleAsync(v => v.Id == variant.Id);
        Assert.Null(saved.DescriptionHtml);
        Assert.False(saved.ShowDescriptionInline);
    }

    [Fact]
    public async Task AnUnknownVariantIsRefused()
    {
        var db = TestCatalogDbContextFactory.Create();

        var result = await new SetVariantDescriptionCommandHandler(db).Handle(
            new SetVariantDescriptionCommand(Guid.NewGuid(), "<p>x</p>", false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.VariantNotFound, result.Error);
    }
}
