using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaqCategory;
using HAMBOX.Modules.Content.Application.Features.Faqs.DeleteFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.DuplicateFaq;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.GetPublishedFaqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.ReorderFaqs;
using HAMBOX.Modules.Content.Application.Features.Faqs.SetFaqPublishState;
using HAMBOX.Modules.Content.Application.Features.Faqs.UpdateFaq;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Content.Faqs;

/// <summary>
/// Covers the FAQ system's scoping/publishing invariants: Global/Product/Category isolation (a
/// product's FAQs never leak to another product), the public query's Global-fallback behavior,
/// publish/unpublish gating, ordering, admin search, and invalid scope/target combinations —
/// both the domain-level guard (<see cref="Faq.Create"/>) and the handler-level Catalog existence
/// check (<see cref="CreateFaqCommandHandler"/>).
/// </summary>
public sealed class FaqScopeTests
{
    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string? UserId => "test-user";
        public string? DisplayName => null;
        public bool IsAuthenticated => true;
        public bool IsAdminContext => true;
    }

    private static readonly ICurrentUserService CurrentUser = new FakeCurrentUserService();

    private static ContentDbContext CreateContentDb()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContentDbContext(options);
    }

    private static TestCatalogDbContext CreateCatalogDb()
    {
        var options = new DbContextOptionsBuilder<TestCatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestCatalogDbContext(options);
    }

    private static async Task<Guid> SeedFaqCategoryAsync(ContentDbContext contentDb)
    {
        var handler = new CreateFaqCategoryCommandHandler(contentDb);
        var result = await handler.Handle(new CreateFaqCategoryCommand("Billing", null), default);
        Assert.True(result.IsSuccess);
        return result.Value.Id;
    }

    private static async Task<Guid> SeedProductAsync(TestCatalogDbContext catalogDb)
    {
        var category = Category.Create("", "Seed Category", $"seed-{Guid.NewGuid():N}");
        catalogDb.Categories.Add(category);
        var product = Product.Create("", "Seed Product", "", "Description", 9.99m, category.Id);
        catalogDb.Products.Add(product);
        await catalogDb.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<Guid> SeedCatalogCategoryAsync(TestCatalogDbContext catalogDb)
    {
        var category = Category.Create("", $"Seed Category {Guid.NewGuid():N}", $"seed-{Guid.NewGuid():N}");
        catalogDb.Categories.Add(category);
        await catalogDb.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<Guid> CreateAndPublishFaqAsync(
        ContentDbContext contentDb, TestCatalogDbContext catalogDb, Guid faqCategoryId,
        string questionEn, FaqScope scope, Guid? targetId, int sortOrder = 0)
    {
        var createHandler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var created = await createHandler.Handle(
            new CreateFaqCommand(questionEn, null, "Answer", null, faqCategoryId, scope, targetId, sortOrder), default);
        Assert.True(created.IsSuccess);

        var publishHandler = new SetFaqPublishStateCommandHandler(contentDb, CurrentUser);
        var published = await publishHandler.Handle(new SetFaqPublishStateCommand(created.Value, true), default);
        Assert.True(published.IsSuccess);

        return created.Value;
    }

    // --- Domain-level invariant (Faq.Create) ---

    [Fact]
    public void Create_GlobalWithTargetId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Faq.Create("Q", null, "A", null, Guid.NewGuid(), FaqScope.Global, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(FaqScope.Product)]
    [InlineData(FaqScope.Category)]
    public void Create_ScopedWithoutTargetId_Throws(FaqScope scope)
    {
        Assert.Throws<ArgumentException>(() =>
            Faq.Create("Q", null, "A", null, Guid.NewGuid(), scope, null));
    }

    // --- Handler-level Catalog existence check ---

    [Fact]
    public async Task CreateFaq_ProductScope_UnknownProductId_Fails()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var handler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await handler.Handle(
            new CreateFaqCommand("Q", null, "A", null, faqCategoryId, FaqScope.Product, Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.FaqTargetProductNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateFaq_CategoryScope_UnknownCategoryId_Fails()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var handler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await handler.Handle(
            new CreateFaqCommand("Q", null, "A", null, faqCategoryId, FaqScope.Category, Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.FaqTargetCategoryNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateFaq_UnknownFaqCategoryId_Fails()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();

        var handler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await handler.Handle(
            new CreateFaqCommand("Q", null, "A", null, Guid.NewGuid(), FaqScope.Global, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.FaqCategoryNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateFaq_ProductScope_ValidProductId_Succeeds()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);
        var productId = await SeedProductAsync(catalogDb);

        var handler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await handler.Handle(
            new CreateFaqCommand("Q", null, "A", null, faqCategoryId, FaqScope.Product, productId), default);

        Assert.True(result.IsSuccess);
    }

    // --- Public query: Global fallback + strict target isolation ---

    [Fact]
    public async Task PublishedFaqs_ProductPage_ReturnsGlobalPlusThatProductOnly_NeverAnotherProducts()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);
        var productA = await SeedProductAsync(catalogDb);
        var productB = await SeedProductAsync(catalogDb);

        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Global Q", FaqScope.Global, null);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Product A Q", FaqScope.Product, productA);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Product B Q", FaqScope.Product, productB);

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var resultForA = await handler.Handle(new GetPublishedFaqsQuery(FaqScope.Product, productA), default);

        Assert.True(resultForA.IsSuccess);
        Assert.Equal(2, resultForA.Value.Count); // Global + Product A only
        Assert.Contains(resultForA.Value, f => f.QuestionEn == "Global Q");
        Assert.Contains(resultForA.Value, f => f.QuestionEn == "Product A Q");
        Assert.DoesNotContain(resultForA.Value, f => f.QuestionEn == "Product B Q"); // Product A never receives Product B's FAQs
    }

    [Fact]
    public async Task PublishedFaqs_CategoryPage_ReturnsGlobalPlusThatCategoryOnly()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);
        var categoryA = await SeedCatalogCategoryAsync(catalogDb);
        var categoryB = await SeedCatalogCategoryAsync(catalogDb);

        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Global Q", FaqScope.Global, null);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Category A Q", FaqScope.Category, categoryA);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Category B Q", FaqScope.Category, categoryB);

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var resultForA = await handler.Handle(new GetPublishedFaqsQuery(FaqScope.Category, categoryA), default);

        Assert.True(resultForA.IsSuccess);
        Assert.Equal(2, resultForA.Value.Count);
        Assert.DoesNotContain(resultForA.Value, f => f.QuestionEn == "Category B Q");
    }

    [Fact]
    public async Task PublishedFaqs_Hub_Global_ReturnsOnlyGlobalFaqs_NotProductOrCategoryScoped()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);
        var productId = await SeedProductAsync(catalogDb);

        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Global Q", FaqScope.Global, null);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Product Q", FaqScope.Product, productId);

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var result = await handler.Handle(new GetPublishedFaqsQuery(), default); // default = Global, no target

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Global Q", result.Value[0].QuestionEn);
    }

    // --- Publishing: drafts never appear publicly ---

    [Fact]
    public async Task PublishedFaqs_UnpublishedFaq_NeverAppears()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var createHandler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        await createHandler.Handle(new CreateFaqCommand("Draft Q", null, "A", null, faqCategoryId, FaqScope.Global, null), default);
        // never published

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var result = await handler.Handle(new GetPublishedFaqsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task PublishedFaqs_UnpublishingAPreviouslyPublishedFaq_RemovesItFromPublicResults()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var faqId = await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Q", FaqScope.Global, null);

        var unpublishHandler = new SetFaqPublishStateCommandHandler(contentDb, CurrentUser);
        await unpublishHandler.Handle(new SetFaqPublishStateCommand(faqId, false), default);

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var result = await handler.Handle(new GetPublishedFaqsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task PublishedFaqs_DeletedFaq_NeverAppearsEvenIfPublished()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var faqId = await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Q", FaqScope.Global, null);

        var deleteHandler = new DeleteFaqCommandHandler(contentDb, CurrentUser);
        var deleted = await deleteHandler.Handle(new DeleteFaqCommand(faqId), default);
        Assert.True(deleted.IsSuccess);

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var result = await handler.Handle(new GetPublishedFaqsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    // --- Ordering ---

    [Fact]
    public async Task PublishedFaqs_RespectSortOrder()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Third", FaqScope.Global, null, sortOrder: 3);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "First", FaqScope.Global, null, sortOrder: 1);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Second", FaqScope.Global, null, sortOrder: 2);

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var result = await handler.Handle(new GetPublishedFaqsQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(["First", "Second", "Third"], result.Value.Select(f => f.QuestionEn));
    }

    [Fact]
    public async Task ReorderFaqs_UpdatesSortOrder_ReflectedInPublicQuery()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var firstId = await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "First", FaqScope.Global, null, sortOrder: 0);
        var secondId = await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Second", FaqScope.Global, null, sortOrder: 1);

        var reorderHandler = new ReorderFaqsCommandHandler(contentDb, CurrentUser);
        var reordered = await reorderHandler.Handle(
            new ReorderFaqsCommand(
            [
                new(secondId, 0),
                new(firstId, 1),
            ]), default);
        Assert.True(reordered.IsSuccess);

        var handler = new GetPublishedFaqsQueryHandler(contentDb);
        var result = await handler.Handle(new GetPublishedFaqsQuery(), default);

        Assert.Equal(["Second", "First"], result.Value.Select(f => f.QuestionEn));
    }

    // --- Admin search ---

    [Fact]
    public async Task GetFaqs_SearchTerm_MatchesQuestionEnCaseInsensitive()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "How do refunds work?", FaqScope.Global, null);
        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "How do I reset my password?", FaqScope.Global, null);

        var handler = new GetFaqsQueryHandler(contentDb, catalogDb);
        var result = await handler.Handle(new GetFaqsQuery(SearchTerm: "refund"), default);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Contains("refunds", result.Value.Items.Single().QuestionEn);
    }

    [Fact]
    public async Task GetFaqs_FilterByScopeAndPublishedState()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);
        var productId = await SeedProductAsync(catalogDb);

        await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Published Global", FaqScope.Global, null);
        var draftCreate = await new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser).Handle(
            new CreateFaqCommand("Draft Product", null, "A", null, faqCategoryId, FaqScope.Product, productId), default);
        Assert.True(draftCreate.IsSuccess);

        var handler = new GetFaqsQueryHandler(contentDb, catalogDb);

        var publishedOnly = await handler.Handle(new GetFaqsQuery(IsPublished: true), default);
        Assert.Single(publishedOnly.Value.Items);
        Assert.Equal("Published Global", publishedOnly.Value.Items.Single().QuestionEn);

        var productScopeOnly = await handler.Handle(new GetFaqsQuery(Scope: FaqScope.Product), default);
        Assert.Single(productScopeOnly.Value.Items);
        Assert.Equal("Draft Product", productScopeOnly.Value.Items.Single().QuestionEn);
        Assert.Equal("Seed Product", productScopeOnly.Value.Items.Single().TargetLabel);
    }

    // --- Duplicate ---

    [Fact]
    public async Task DuplicateFaq_CreatesUnpublishedCopy_OriginalUnaffected()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var originalId = await CreateAndPublishFaqAsync(contentDb, catalogDb, faqCategoryId, "Original", FaqScope.Global, null);

        var duplicateHandler = new DuplicateFaqCommandHandler(contentDb, CurrentUser);
        var duplicated = await duplicateHandler.Handle(new DuplicateFaqCommand(originalId), default);
        Assert.True(duplicated.IsSuccess);
        Assert.NotEqual(originalId, duplicated.Value);

        var copy = await contentDb.Faqs.SingleAsync(f => f.Id == duplicated.Value);
        Assert.Equal("Original (Copy)", copy.QuestionEn);
        Assert.False(copy.IsPublished);

        var original = await contentDb.Faqs.SingleAsync(f => f.Id == originalId);
        Assert.True(original.IsPublished); // unaffected by duplicating it
    }

    // --- Update: switching scope re-validates target ---

    [Fact]
    public async Task UpdateFaq_SwitchingToProductScope_WithUnknownProductId_Fails()
    {
        using var contentDb = CreateContentDb();
        using var catalogDb = CreateCatalogDb();
        var faqCategoryId = await SeedFaqCategoryAsync(contentDb);

        var createHandler = new CreateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var created = await createHandler.Handle(
            new CreateFaqCommand("Q", null, "A", null, faqCategoryId, FaqScope.Global, null), default);

        var updateHandler = new UpdateFaqCommandHandler(contentDb, catalogDb, CurrentUser);
        var result = await updateHandler.Handle(
            new UpdateFaqCommand(created.Value, "Q", null, "A", null, faqCategoryId, FaqScope.Product, Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.FaqTargetProductNotFound.Code, result.Error.Code);
    }
}
