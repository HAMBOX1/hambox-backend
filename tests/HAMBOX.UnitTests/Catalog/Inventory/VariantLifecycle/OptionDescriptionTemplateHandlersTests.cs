using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Covers the reusable Option Description Template lifecycle: save a description snapshot once,
/// copy it into multiple products' <see cref="ProductOption.DescriptionHtml"/>, and confirm
/// editing/deleting the template never propagates back to a product option that already copied
/// from it — mirrors <see cref="OptionGroupTemplateHandlersTests"/>'s isolation guarantees.
/// </summary>
public sealed class OptionDescriptionTemplateHandlersTests
{
    private static ProductOption CreateOptionInNewGroup(TestCatalogDbContext db, Guid productId, string value, string label)
    {
        var group = ProductOptionGroup.Create(productId, $"group-{Guid.NewGuid():N}", "Region", sortOrder: 0);
        var option = group.AddOption(value, label, 0);
        db.ProductOptionGroups.Add(group);
        return option;
    }

    [Fact]
    public async Task Create_Succeeds_AndSanitizesContent()
    {
        var db = TestCatalogDbContextFactory.Create();
        var handler = new CreateOptionDescriptionTemplateCommandHandler(db);

        var result = await handler.Handle(
            new CreateOptionDescriptionTemplateCommand("Global Activation Instructions", "<p>Works worldwide.</p><script>alert(1)</script>"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await db.OptionDescriptionTemplates.SingleAsync(t => t.Id == result.Value);
        Assert.Equal("Global Activation Instructions", saved.Name);
        Assert.Equal("<p>Works worldwide.</p>", saved.DescriptionHtml);
    }

    [Fact]
    public async Task Create_EmptyName_Throws()
    {
        var db = TestCatalogDbContextFactory.Create();
        var handler = new CreateOptionDescriptionTemplateCommandHandler(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new CreateOptionDescriptionTemplateCommand("   ", "<p>Content</p>"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_DuplicateName_Fails()
    {
        var db = TestCatalogDbContextFactory.Create();
        var handler = new CreateOptionDescriptionTemplateCommandHandler(db);

        var first = await handler.Handle(new CreateOptionDescriptionTemplateCommand("Global Instructions", "<p>v1</p>"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await handler.Handle(new CreateOptionDescriptionTemplateCommand("Global Instructions", "<p>v2</p>"), CancellationToken.None);
        Assert.True(second.IsFailure);
        Assert.Equal(CatalogErrors.DuplicateOptionDescriptionTemplateName.Code, second.Error.Code);
    }

    [Fact]
    public async Task Create_SanitizedEmptyContent_FailsCleanly()
    {
        var db = TestCatalogDbContextFactory.Create();
        var handler = new CreateOptionDescriptionTemplateCommandHandler(db);

        var result = await handler.Handle(
            new CreateOptionDescriptionTemplateCommand("Empty After Sanitize", "<script>alert(1)</script><style>.x{}</style>"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.OptionDescriptionTemplateContentRequired.Code, result.Error.Code);
        Assert.False(await db.OptionDescriptionTemplates.AnyAsync());
    }

    [Fact]
    public async Task Create_StripsUnsafeUrlSchemesAndPreservesAllowedFormatting()
    {
        var db = TestCatalogDbContextFactory.Create();
        var handler = new CreateOptionDescriptionTemplateCommandHandler(db);

        var result = await handler.Handle(
            new CreateOptionDescriptionTemplateCommand(
                "Xbox Redemption Instructions",
                "<p><strong>Redeem</strong> at <a href=\"javascript:alert(1)\">this link</a> or <a href=\"https://xbox.com\">here</a>.</p><div>ignored wrapper</div>"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await db.OptionDescriptionTemplates.SingleAsync(t => t.Id == result.Value);
        Assert.Contains("<strong>Redeem</strong>", saved.DescriptionHtml);
        Assert.Contains("href=\"https://xbox.com\"", saved.DescriptionHtml);
        Assert.DoesNotContain("javascript:", saved.DescriptionHtml);
        Assert.DoesNotContain("<div>", saved.DescriptionHtml);
        Assert.Contains("ignored wrapper", saved.DescriptionHtml);
    }

    [Fact]
    public async Task Search_ReturnsMatchesByNameSubstring()
    {
        var db = TestCatalogDbContextFactory.Create();
        var createHandler = new CreateOptionDescriptionTemplateCommandHandler(db);
        await createHandler.Handle(new CreateOptionDescriptionTemplateCommand("Global Activation Instructions", "<p>a</p>"), CancellationToken.None);
        await createHandler.Handle(new CreateOptionDescriptionTemplateCommand("Xbox Redemption Instructions", "<p>b</p>"), CancellationToken.None);

        var searchHandler = new SearchOptionDescriptionTemplatesQueryHandler(db);
        var result = await searchHandler.Handle(new SearchOptionDescriptionTemplatesQuery("Global"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Global Activation Instructions", result.Value[0].Name);
    }

    [Fact]
    public async Task GetById_NotFound_Fails()
    {
        var db = TestCatalogDbContextFactory.Create();
        var handler = new GetOptionDescriptionTemplateQueryHandler(db);

        var result = await handler.Handle(new GetOptionDescriptionTemplateQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.OptionDescriptionTemplateNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_ChangesNameAndContent()
    {
        var db = TestCatalogDbContextFactory.Create();
        var createResult = await new CreateOptionDescriptionTemplateCommandHandler(db).Handle(
            new CreateOptionDescriptionTemplateCommand("Global Instructions v1", "<p>Works worldwide.</p>"), CancellationToken.None);
        var templateId = createResult.Value;

        var updateHandler = new UpdateOptionDescriptionTemplateCommandHandler(db);
        var updateResult = await updateHandler.Handle(
            new UpdateOptionDescriptionTemplateCommand(templateId, "Global Instructions v2", "<p>Works worldwide. Updated.</p>"), CancellationToken.None);

        Assert.True(updateResult.IsSuccess);
        var saved = await db.OptionDescriptionTemplates.AsNoTracking().SingleAsync(t => t.Id == templateId);
        Assert.Equal("Global Instructions v2", saved.Name);
        Assert.Equal("<p>Works worldwide. Updated.</p>", saved.DescriptionHtml);
    }

    [Fact]
    public async Task Delete_RemovesTemplate()
    {
        var db = TestCatalogDbContextFactory.Create();
        var createResult = await new CreateOptionDescriptionTemplateCommandHandler(db).Handle(
            new CreateOptionDescriptionTemplateCommand("Global Instructions", "<p>Works worldwide.</p>"), CancellationToken.None);

        var deleteResult = await new DeleteOptionDescriptionTemplateCommandHandler(db).Handle(
            new DeleteOptionDescriptionTemplateCommand(createResult.Value), CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        Assert.False(await db.OptionDescriptionTemplates.AnyAsync(t => t.Id == createResult.Value));
    }

    [Fact]
    public async Task CopyToProductOption_TwoProductsIndependentlyUseSameTemplate_ThenDiverge()
    {
        var db = TestCatalogDbContextFactory.Create();

        var createResult = await new CreateOptionDescriptionTemplateCommandHandler(db).Handle(
            new CreateOptionDescriptionTemplateCommand("Global Instructions v1", "<p>Works worldwide.</p>"), CancellationToken.None);
        var templateId = createResult.Value;
        var template = await db.OptionDescriptionTemplates.AsNoTracking().SingleAsync(t => t.Id == templateId);

        // "Applying" a saved description is just copying its sanitized HTML into the normal
        // product-option create/update flow — there is no dedicated apply endpoint.
        var currentUser = new FakeCurrentUserService("admin-1");
        var optionA = CreateOptionInNewGroup(db, Guid.NewGuid(), "global", "Global");
        var optionB = CreateOptionInNewGroup(db, Guid.NewGuid(), "global", "Global");
        await db.SaveChangesAsync(CancellationToken.None);

        var updateHandler = new UpdateProductOptionCommandHandler(db);
        await updateHandler.Handle(new UpdateProductOptionCommand(optionA.Id, optionA.Label, optionA.SortOrder, template.DescriptionHtml), CancellationToken.None);
        await updateHandler.Handle(new UpdateProductOptionCommand(optionB.Id, optionB.Label, optionB.SortOrder, template.DescriptionHtml), CancellationToken.None);

        var reloadedA = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionA.Id);
        var reloadedB = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionB.Id);
        Assert.Equal(template.DescriptionHtml, reloadedA.DescriptionHtml);
        Assert.Equal(template.DescriptionHtml, reloadedB.DescriptionHtml);

        // Product A modifies its copy — Product B is untouched (independent copies, not a live reference).
        await updateHandler.Handle(new UpdateProductOptionCommand(optionA.Id, optionA.Label, optionA.SortOrder, "<p>Modified by Product A only.</p>"), CancellationToken.None);
        var modifiedA = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionA.Id);
        var stillB = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionB.Id);
        Assert.Equal("<p>Modified by Product A only.</p>", modifiedA.DescriptionHtml);
        Assert.Equal(template.DescriptionHtml, stillB.DescriptionHtml);
        Assert.NotEqual(modifiedA.DescriptionHtml, stillB.DescriptionHtml);

        // Editing the saved template afterward affects neither A nor B.
        await new UpdateOptionDescriptionTemplateCommandHandler(db).Handle(
            new UpdateOptionDescriptionTemplateCommand(templateId, "Global Instructions v2", "<p>Works worldwide. v2.</p>"), CancellationToken.None);
        var afterTemplateEditA = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionA.Id);
        var afterTemplateEditB = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionB.Id);
        Assert.Equal("<p>Modified by Product A only.</p>", afterTemplateEditA.DescriptionHtml);
        Assert.Equal(template.DescriptionHtml, afterTemplateEditB.DescriptionHtml);

        // Deleting the saved template afterward affects neither A nor B.
        await new DeleteOptionDescriptionTemplateCommandHandler(db).Handle(new DeleteOptionDescriptionTemplateCommand(templateId), CancellationToken.None);
        Assert.False(await db.OptionDescriptionTemplates.AnyAsync(t => t.Id == templateId));
        var afterDeleteA = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionA.Id);
        var afterDeleteB = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == optionB.Id);
        Assert.Equal("<p>Modified by Product A only.</p>", afterDeleteA.DescriptionHtml);
        Assert.Equal(template.DescriptionHtml, afterDeleteB.DescriptionHtml);
    }

    [Fact]
    public async Task ExistingProductOptionDescriptionBehavior_RemainsUnchanged()
    {
        // Regression guard: CreateProductOptionCommandHandler/UpdateProductOptionCommandHandler
        // and ProductOptionDescriptionSanitizer are untouched by this feature — direct product
        // option description writes must sanitize exactly as before.
        var db = TestCatalogDbContextFactory.Create();
        var currentUser = new FakeCurrentUserService("admin-1");
        var group = ProductOptionGroup.Create(Guid.NewGuid(), "region", "Region", sortOrder: 0);
        db.ProductOptionGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var createHandler = new CreateProductOptionCommandHandler(db, currentUser);
        var createResult = await createHandler.Handle(
            new CreateProductOptionCommand(group.Id, "eu", "Europe", 0, "<p>EU only.</p><script>alert(1)</script>"),
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);
        var option = await db.ProductOptions.AsNoTracking().SingleAsync(o => o.Id == createResult.Value);
        Assert.Equal("<p>EU only.</p>", option.DescriptionHtml);
    }
}
