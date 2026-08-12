using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Covers the reusable Option Group Template lifecycle: save an existing product option group as a
/// template, import it into other products as independent snapshots, and confirm editing/deleting
/// the template never propagates back to products that already imported it.
/// </summary>
public sealed class OptionGroupTemplateHandlersTests
{
    private static (ProductOptionGroup Group, ProductOption Global, ProductOption Us) CreateXboxRegionGroup(Guid productId)
    {
        var group = ProductOptionGroup.Create(productId, "xbox-region", "Xbox Region", sortOrder: 0);
        var global = group.AddOption("global", "Global", 0);
        var us = group.AddOption("us", "United States", 1);
        return (group, global, us);
    }

    private static ImportOptionGroupTemplateCommandHandler CreateImportHandler(TestCatalogDbContext db, ISender sender) =>
        new(db, sender, new FakeCurrentUserService("admin-1"));

    [Fact]
    public async Task SaveAsTemplate_ThenImport_CreatesIndependentProductOptionGroup()
    {
        var db = TestCatalogDbContextFactory.Create();
        var productA = Guid.NewGuid();
        var (group, global, us) = CreateXboxRegionGroup(productA);
        global.Update("Global", 0, "<p>Works worldwide.</p>");
        us.Update("United States", 1, null);
        db.ProductOptionGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var saveHandler = new SaveOptionGroupAsTemplateCommandHandler(db);
        var saveResult = await saveHandler.Handle(new SaveOptionGroupAsTemplateCommand(group.Id, "Xbox Region"), CancellationToken.None);
        Assert.True(saveResult.IsSuccess);

        var template = await db.OptionGroupTemplates.Include(t => t.Options).SingleAsync(t => t.Id == saveResult.Value);
        Assert.Equal("Xbox Region", template.Name);
        Assert.Equal(2, template.Options.Count);
        Assert.Contains(template.Options, o => o.Value == "global" && o.DescriptionHtml == "<p>Works worldwide.</p>");

        var productB = Guid.NewGuid();
        var sender = new DispatchingFakeSender<DeleteProductOptionGroupCommand, Result>(
            new DeleteProductOptionGroupCommandHandler(db, new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), new FakeCommerceVariantUsageProvider()), new FakeCommerceVariantUsageProvider()));
        var importHandler = CreateImportHandler(db, sender);

        var importResult = await importHandler.Handle(new ImportOptionGroupTemplateCommand(productB, template.Id, ImportConflictResolution.AddSeparate), CancellationToken.None);
        Assert.True(importResult.IsSuccess);

        var importedGroup = await db.ProductOptionGroups.Include(g => g.Options).SingleAsync(g => g.Id == importResult.Value);
        Assert.Equal(productB, importedGroup.ProductId);
        Assert.Equal("xbox-region", importedGroup.Key);
        Assert.Equal(2, importedGroup.Options.Count);

        // Ordering preserved.
        var orderedLabels = importedGroup.Options.OrderBy(o => o.SortOrder).Select(o => o.Label).ToList();
        Assert.Equal(["Global", "United States"], orderedLabels);

        // Descriptions preserved.
        Assert.Equal("<p>Works worldwide.</p>", importedGroup.Options.Single(o => o.Value == "global").DescriptionHtml);

        // Independent records — not the same rows as product A's group.
        Assert.NotEqual(group.Id, importedGroup.Id);
    }

    [Fact]
    public async Task ModifyingImportedGroup_DoesNotAffectTemplateOrOtherImports()
    {
        var db = TestCatalogDbContextFactory.Create();
        var sourceProduct = Guid.NewGuid();
        var (group, _, _) = CreateXboxRegionGroup(sourceProduct);
        db.ProductOptionGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var saveResult = await new SaveOptionGroupAsTemplateCommandHandler(db).Handle(
            new SaveOptionGroupAsTemplateCommand(group.Id, "Xbox Region"), CancellationToken.None);
        var templateId = saveResult.Value;

        var sender = new DispatchingFakeSender<DeleteProductOptionGroupCommand, Result>(
            new DeleteProductOptionGroupCommandHandler(db, new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), new FakeCommerceVariantUsageProvider()), new FakeCommerceVariantUsageProvider()));
        var importHandler = CreateImportHandler(db, sender);

        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var importA = await importHandler.Handle(new ImportOptionGroupTemplateCommand(productA, templateId, ImportConflictResolution.AddSeparate), CancellationToken.None);
        var importB = await importHandler.Handle(new ImportOptionGroupTemplateCommand(productB, templateId, ImportConflictResolution.AddSeparate), CancellationToken.None);

        // Mutate product A's imported group.
        var groupA = await db.ProductOptionGroups.Include(g => g.Options).SingleAsync(g => g.Id == importA.Value);
        groupA.Options.First().Update("Renamed by Product A", 0, null);
        await db.SaveChangesAsync(CancellationToken.None);

        // Template unchanged.
        var reloadedTemplate = await db.OptionGroupTemplates.AsNoTracking().Include(t => t.Options).SingleAsync(t => t.Id == templateId);
        Assert.DoesNotContain(reloadedTemplate.Options, o => o.Label == "Renamed by Product A");

        // Product B's independent import unchanged.
        var groupB = await db.ProductOptionGroups.AsNoTracking().Include(g => g.Options).SingleAsync(g => g.Id == importB.Value);
        Assert.DoesNotContain(groupB.Options, o => o.Label == "Renamed by Product A");
        Assert.Contains(groupB.Options, o => o.Label == "Global");
    }

    [Fact]
    public async Task EditingTemplate_DoesNotChangeAlreadyImportedProducts()
    {
        var db = TestCatalogDbContextFactory.Create();
        var sourceProduct = Guid.NewGuid();
        var (group, _, _) = CreateXboxRegionGroup(sourceProduct);
        db.ProductOptionGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var saveResult = await new SaveOptionGroupAsTemplateCommandHandler(db).Handle(
            new SaveOptionGroupAsTemplateCommand(group.Id, "Xbox Region"), CancellationToken.None);
        var templateId = saveResult.Value;

        var sender = new DispatchingFakeSender<DeleteProductOptionGroupCommand, Result>(
            new DeleteProductOptionGroupCommandHandler(db, new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), new FakeCommerceVariantUsageProvider()), new FakeCommerceVariantUsageProvider()));
        var importHandler = CreateImportHandler(db, sender);

        var productA = Guid.NewGuid();
        var importA = await importHandler.Handle(new ImportOptionGroupTemplateCommand(productA, templateId, ImportConflictResolution.AddSeparate), CancellationToken.None);

        // Later: admin edits the template, adding "UK".
        var updateHandler = new UpdateOptionGroupTemplateCommandHandler(db);
        var updateResult = await updateHandler.Handle(new UpdateOptionGroupTemplateCommand(
            templateId,
            "Xbox Region",
            IsRequiredDefault: true,
            Options:
            [
                new OptionGroupTemplateOptionInput("global", "Global", 0, null),
                new OptionGroupTemplateOptionInput("us", "United States", 1, null),
                new OptionGroupTemplateOptionInput("uk", "United Kingdom", 2, null),
            ]), CancellationToken.None);
        Assert.True(updateResult.IsSuccess);

        // Product A, already imported, still has exactly its original two values.
        var groupA = await db.ProductOptionGroups.AsNoTracking().Include(g => g.Options).SingleAsync(g => g.Id == importA.Value);
        Assert.Equal(2, groupA.Options.Count);
        Assert.DoesNotContain(groupA.Options, o => o.Value == "uk");
    }

    [Fact]
    public async Task DeletingTemplate_DoesNotBreakAlreadyImportedProductGroup()
    {
        var db = TestCatalogDbContextFactory.Create();
        var sourceProduct = Guid.NewGuid();
        var (group, _, _) = CreateXboxRegionGroup(sourceProduct);
        db.ProductOptionGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var saveResult = await new SaveOptionGroupAsTemplateCommandHandler(db).Handle(
            new SaveOptionGroupAsTemplateCommand(group.Id, "Xbox Region"), CancellationToken.None);
        var templateId = saveResult.Value;

        var sender = new DispatchingFakeSender<DeleteProductOptionGroupCommand, Result>(
            new DeleteProductOptionGroupCommandHandler(db, new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), new FakeCommerceVariantUsageProvider()), new FakeCommerceVariantUsageProvider()));
        var importHandler = CreateImportHandler(db, sender);

        var productA = Guid.NewGuid();
        var importA = await importHandler.Handle(new ImportOptionGroupTemplateCommand(productA, templateId, ImportConflictResolution.AddSeparate), CancellationToken.None);

        var deleteHandler = new DeleteOptionGroupTemplateCommandHandler(db);
        var deleteResult = await deleteHandler.Handle(new DeleteOptionGroupTemplateCommand(templateId), CancellationToken.None);
        Assert.True(deleteResult.IsSuccess);
        Assert.False(await db.OptionGroupTemplates.AnyAsync(t => t.Id == templateId));

        var groupA = await db.ProductOptionGroups.Include(g => g.Options).SingleAsync(g => g.Id == importA.Value);
        Assert.Equal(2, groupA.Options.Count);
    }

    [Fact]
    public async Task Import_NameCollisionWithAddSeparate_AutoSuffixesKey()
    {
        var db = TestCatalogDbContextFactory.Create();
        var sourceProduct = Guid.NewGuid();
        var (group, _, _) = CreateXboxRegionGroup(sourceProduct);
        db.ProductOptionGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var saveResult = await new SaveOptionGroupAsTemplateCommandHandler(db).Handle(
            new SaveOptionGroupAsTemplateCommand(group.Id, "Xbox Region"), CancellationToken.None);
        var templateId = saveResult.Value;

        var sender = new DispatchingFakeSender<DeleteProductOptionGroupCommand, Result>(
            new DeleteProductOptionGroupCommandHandler(db, new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), new FakeCommerceVariantUsageProvider()), new FakeCommerceVariantUsageProvider()));
        var importHandler = CreateImportHandler(db, sender);

        // Import into a fresh product (no pre-existing groups) twice with AddSeparate — the
        // source product already has its own original "xbox-region" group (the one the template
        // was saved from), which would otherwise collide on the first import too.
        var targetProduct = Guid.NewGuid();
        var first = await importHandler.Handle(new ImportOptionGroupTemplateCommand(targetProduct, templateId, ImportConflictResolution.AddSeparate), CancellationToken.None);
        var second = await importHandler.Handle(new ImportOptionGroupTemplateCommand(targetProduct, templateId, ImportConflictResolution.AddSeparate), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstGroup = await db.ProductOptionGroups.SingleAsync(g => g.Id == first.Value);
        var secondGroup = await db.ProductOptionGroups.SingleAsync(g => g.Id == second.Value);
        Assert.Equal("xbox-region", firstGroup.Key);
        Assert.Equal("xbox-region-2", secondGroup.Key);
    }

    [Fact]
    public async Task Import_WithReplace_RemovesExistingGroupAndImportsFresh()
    {
        var db = TestCatalogDbContextFactory.Create();
        var sourceProduct = Guid.NewGuid();
        var (group, _, _) = CreateXboxRegionGroup(sourceProduct);
        db.ProductOptionGroups.Add(group);
        await db.SaveChangesAsync(CancellationToken.None);

        var saveResult = await new SaveOptionGroupAsTemplateCommandHandler(db).Handle(
            new SaveOptionGroupAsTemplateCommand(group.Id, "Xbox Region"), CancellationToken.None);
        var templateId = saveResult.Value;

        var sender = new DispatchingFakeSender<DeleteProductOptionGroupCommand, Result>(
            new DeleteProductOptionGroupCommandHandler(db, new InventoryEngine(db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), new FakeCommerceVariantUsageProvider()), new FakeCommerceVariantUsageProvider()));
        var importHandler = CreateImportHandler(db, sender);

        // Import into the same product with Replace — should delete the original "xbox-region"
        // group (the exact one saved as the template) and create a fresh one in its place.
        var result = await importHandler.Handle(new ImportOptionGroupTemplateCommand(sourceProduct, templateId, ImportConflictResolution.Replace), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(group.Id, result.Value);
        Assert.False(await db.ProductOptionGroups.AnyAsync(g => g.Id == group.Id));
        var replacementGroup = await db.ProductOptionGroups.SingleAsync(g => g.ProductId == sourceProduct && g.Key == "xbox-region");
        Assert.Equal(result.Value, replacementGroup.Id);
    }

    [Fact]
    public async Task SaveAsTemplate_DuplicateName_Fails()
    {
        var db = TestCatalogDbContextFactory.Create();
        var productId = Guid.NewGuid();
        var (group1, _, _) = CreateXboxRegionGroup(productId);
        db.ProductOptionGroups.Add(group1);
        await db.SaveChangesAsync(CancellationToken.None);

        var saveHandler = new SaveOptionGroupAsTemplateCommandHandler(db);
        var firstSave = await saveHandler.Handle(new SaveOptionGroupAsTemplateCommand(group1.Id, "Xbox Region"), CancellationToken.None);
        Assert.True(firstSave.IsSuccess);

        var group2 = ProductOptionGroup.Create(productId, "xbox-region-2", "Xbox Region", sortOrder: 1);
        group2.AddOption("global", "Global", 0);
        db.ProductOptionGroups.Add(group2);
        await db.SaveChangesAsync(CancellationToken.None);

        var secondSave = await saveHandler.Handle(new SaveOptionGroupAsTemplateCommand(group2.Id, "Xbox Region"), CancellationToken.None);
        Assert.True(secondSave.IsFailure);
        Assert.Equal(CatalogErrors.DuplicateOptionGroupTemplateName.Code, secondSave.Error.Code);
    }
}
