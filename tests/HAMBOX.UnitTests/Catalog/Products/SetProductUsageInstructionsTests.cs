using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

namespace HAMBOX.UnitTests.Catalog.Products;

/// <summary>A product's own public "How to use" text: saved sanitized, cleared when empty, bounded in size.</summary>
public sealed class SetProductUsageInstructionsTests
{
    private static async Task<(SetProductUsageInstructionsCommandHandler Handler, TestCatalogDbContext Db, Product Product)> SetupAsync()
    {
        var db = TestCatalogDbContextFactory.Create();
        var product = Product.Create("منتج", "Steam Gift 20", "d", "d", 20m, Guid.NewGuid());
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (new SetProductUsageInstructionsCommandHandler(db), db, product);
    }

    [Fact]
    public async Task SavesTheText_SoTheProductPageCanShowIt()
    {
        var (handler, _, product) = await SetupAsync();

        var result = await handler.Handle(
            new SetProductUsageInstructionsCommand(product.Id, "<p>1. Open <strong>Steam</strong></p><ol><li>Redeem</li></ol>"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("<strong>Steam</strong>", product.UsageInstructionsHtml);
        Assert.Contains("<li>Redeem</li>", product.UsageInstructionsHtml);
    }

    [Fact]
    public async Task StripsScriptsAndEventHandlers_BecauseEveryVisitorSeesTheText()
    {
        var (handler, _, product) = await SetupAsync();

        await handler.Handle(
            new SetProductUsageInstructionsCommand(product.Id, "<p onclick=\"steal()\">Hi</p><script>alert(1)</script><img src=x onerror=alert(2)>"),
            CancellationToken.None);

        Assert.DoesNotContain("<script", product.UsageInstructionsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", product.UsageInstructionsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", product.UsageInstructionsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hi", product.UsageInstructionsHtml);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    public async Task EmptyText_ClearsIt_SoThePageGoesBackToTheDefaultSteps(string? html)
    {
        var (handler, _, product) = await SetupAsync();
        await handler.Handle(new SetProductUsageInstructionsCommand(product.Id, "<p>Something</p>"), CancellationToken.None);
        Assert.NotNull(product.UsageInstructionsHtml);

        var result = await handler.Handle(new SetProductUsageInstructionsCommand(product.Id, html), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(product.UsageInstructionsHtml);
    }

    [Fact]
    public async Task RefusesAnUnknownProduct_AndAnOversizedText()
    {
        var (handler, _, product) = await SetupAsync();

        var unknown = await handler.Handle(new SetProductUsageInstructionsCommand(Guid.NewGuid(), "<p>x</p>"), CancellationToken.None);
        var tooLong = await handler.Handle(
            new SetProductUsageInstructionsCommand(product.Id, new string('a', SetProductUsageInstructionsCommandHandler.MaxLength + 1)),
            CancellationToken.None);

        Assert.Equal(CatalogErrors.ProductNotFound.Code, unknown.Error.Code);
        Assert.Equal(CatalogErrors.UsageInstructionsTooLong.Code, tooLong.Error.Code);
        Assert.Null(product.UsageInstructionsHtml);
    }
}
