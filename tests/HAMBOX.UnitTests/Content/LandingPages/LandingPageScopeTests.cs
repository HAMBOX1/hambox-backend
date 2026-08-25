using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Errors;
using HAMBOX.Modules.Content.Application.Features.LandingPages.ActivateLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.CreateLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.DeleteLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.GetPublishedLandingPage;
using HAMBOX.Modules.Content.Application.Features.LandingPages.PublishLandingPageTemplate;
using HAMBOX.Modules.Content.Application.Features.LandingPages.SaveLandingPageTemplateDraft;
using HAMBOX.Modules.Content.Domain.LandingPages;
using HAMBOX.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Content.LandingPages;

/// <summary>
/// Covers the Product/Category page-scoping extension to the Page Builder's <c>LandingPageTemplate</c>:
/// activate/delete/publish scoping and, most importantly, that a published page for one target can
/// never resolve for a different target (or leak while still a draft). Each test gets its own
/// InMemory database, so tests never interfere with each other.
/// </summary>
public sealed class LandingPageScopeTests
{
    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string? UserId => "test-user";
        public string? DisplayName => null;
        public bool IsAuthenticated => true;
        public bool IsAdminContext => true;
    }

    private static ContentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContentDbContext(options);
    }

    private static readonly ICurrentUserService CurrentUser = new FakeCurrentUserService();

    private static async Task<LandingPageTemplateDetailDto> CreateAndPublishAsync(
        ContentDbContext db, LandingPageScope scope, Guid? targetId, string name = "Test Page")
    {
        var createHandler = new CreateLandingPageTemplateCommandHandler(db, CurrentUser);
        var created = await createHandler.Handle(new CreateLandingPageTemplateCommand(name, null, scope, targetId), default);
        Assert.True(created.IsSuccess);

        var sections = new[] { new LandingPageSectionEntry(Guid.NewGuid(), "Hero", "kinetic-glass", 0, true, "{}") };
        var saveHandler = new SaveLandingPageTemplateDraftCommandHandler(db, CurrentUser);
        await saveHandler.Handle(new SaveLandingPageTemplateDraftCommand(created.Value.Id, sections), default);

        var publishHandler = new PublishLandingPageTemplateCommandHandler(db, CurrentUser);
        var published = await publishHandler.Handle(new PublishLandingPageTemplateCommand(created.Value.Id), default);
        Assert.True(published.IsSuccess);

        var activateHandler = new ActivateLandingPageTemplateCommandHandler(db, CurrentUser);
        var activated = await activateHandler.Handle(new ActivateLandingPageTemplateCommand(created.Value.Id), default);
        Assert.True(activated.IsSuccess);

        return published.Value;
    }

    [Fact]
    public async Task Homepage_ActivateAndDelete_BehaveExactlyAsBeforeScopingWasAdded()
    {
        using var db = CreateDbContext();

        // Seed the way LandingPageDataSeeder does: one Homepage template, activated.
        await CreateAndPublishAsync(db, LandingPageScope.Homepage, null, "Default");

        // A second homepage template can be created and activated, deactivating the first — unchanged behavior.
        var second = await CreateAndPublishAsync(db, LandingPageScope.Homepage, null, "Alternate");
        var all = await db.LandingPageTemplates.ToListAsync();
        Assert.Single(all, t => t.IsActive);
        Assert.Equal(second.Id, all.Single(t => t.IsActive).Id);

        // Cannot delete the last remaining homepage template.
        var firstId = all.Single(t => t.Id != second.Id).Id;
        var deleteHandler = new DeleteLandingPageTemplateCommandHandler(db, CurrentUser);
        var deleteFirst = await deleteHandler.Handle(new DeleteLandingPageTemplateCommand(firstId), default);
        Assert.True(deleteFirst.IsSuccess); // two templates exist, so deleting the inactive one is fine

        var deleteLast = await deleteHandler.Handle(new DeleteLandingPageTemplateCommand(second.Id), default);
        Assert.True(deleteLast.IsFailure);
        Assert.Equal(ContentErrors.CannotDeleteActiveTemplate.Code, deleteLast.Error.Code); // active guard fires first
    }

    [Theory]
    [InlineData(LandingPageScope.Product)]
    [InlineData(LandingPageScope.Category)]
    public async Task Target_WithNoPage_ResolvesToNoActiveTemplate(LandingPageScope scope)
    {
        using var db = CreateDbContext();
        var targetId = Guid.NewGuid();

        var handler = new GetPublishedLandingPageQueryHandler(db);
        var result = await handler.Handle(new GetPublishedLandingPageQuery(scope, targetId), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.NoActiveTemplate.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(LandingPageScope.Product)]
    [InlineData(LandingPageScope.Category)]
    public async Task Target_WithDraftOnly_StillResolvesToNoActiveTemplate(LandingPageScope scope)
    {
        using var db = CreateDbContext();
        var targetId = Guid.NewGuid();

        var createHandler = new CreateLandingPageTemplateCommandHandler(db, CurrentUser);
        var created = await createHandler.Handle(new CreateLandingPageTemplateCommand("Draft Page", null, scope, targetId), default);

        var sections = new[] { new LandingPageSectionEntry(Guid.NewGuid(), "Hero", "kinetic-glass", 0, true, "{}") };
        var saveHandler = new SaveLandingPageTemplateDraftCommandHandler(db, CurrentUser);
        await saveHandler.Handle(new SaveLandingPageTemplateDraftCommand(created.Value.Id, sections), default);
        // Never published, never activated.

        var publishedHandler = new GetPublishedLandingPageQueryHandler(db);
        var result = await publishedHandler.Handle(new GetPublishedLandingPageQuery(scope, targetId), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.NoActiveTemplate.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(LandingPageScope.Product)]
    [InlineData(LandingPageScope.Category)]
    public async Task Target_WithPublishedActivePage_Resolves(LandingPageScope scope)
    {
        using var db = CreateDbContext();
        var targetId = Guid.NewGuid();

        await CreateAndPublishAsync(db, scope, targetId);

        var publishedHandler = new GetPublishedLandingPageQueryHandler(db);
        var result = await publishedHandler.Handle(new GetPublishedLandingPageQuery(scope, targetId), default);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Sections);
    }

    [Theory]
    [InlineData(LandingPageScope.Product)]
    [InlineData(LandingPageScope.Category)]
    public async Task PublishedPageForTargetA_NeverResolvesForTargetB(LandingPageScope scope)
    {
        using var db = CreateDbContext();
        var targetA = Guid.NewGuid();
        var targetB = Guid.NewGuid();

        await CreateAndPublishAsync(db, scope, targetA, "Page For A");
        // targetB has no page at all.

        var publishedHandler = new GetPublishedLandingPageQueryHandler(db);

        var resultA = await publishedHandler.Handle(new GetPublishedLandingPageQuery(scope, targetA), default);
        Assert.True(resultA.IsSuccess);

        var resultB = await publishedHandler.Handle(new GetPublishedLandingPageQuery(scope, targetB), default);
        Assert.True(resultB.IsFailure);
        Assert.Equal(ContentErrors.NoActiveTemplate.Code, resultB.Error.Code);
    }

    [Fact]
    public async Task ActivatingProductA_DoesNotDeactivateProductBsPage()
    {
        using var db = CreateDbContext();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        await CreateAndPublishAsync(db, LandingPageScope.Product, productA, "A's Page");
        await CreateAndPublishAsync(db, LandingPageScope.Product, productB, "B's Page");

        var publishedHandler = new GetPublishedLandingPageQueryHandler(db);
        var resultA = await publishedHandler.Handle(new GetPublishedLandingPageQuery(LandingPageScope.Product, productA), default);
        var resultB = await publishedHandler.Handle(new GetPublishedLandingPageQuery(LandingPageScope.Product, productB), default);

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
    }

    [Fact]
    public async Task Deactivating_RestoresNoActiveTemplateResolution()
    {
        using var db = CreateDbContext();
        var productId = Guid.NewGuid();

        var page = await CreateAndPublishAsync(db, LandingPageScope.Product, productId);

        var template = await db.LandingPageTemplates.SingleAsync(t => t.Id == page.Id);
        template.Deactivate();
        await db.SaveChangesAsync();

        var publishedHandler = new GetPublishedLandingPageQueryHandler(db);
        var result = await publishedHandler.Handle(new GetPublishedLandingPageQuery(LandingPageScope.Product, productId), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ContentErrors.NoActiveTemplate.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(LandingPageScope.Product)]
    [InlineData(LandingPageScope.Category)]
    public async Task Target_DeletingTheOnlyPage_IsAllowed_UnlikeHomepage(LandingPageScope scope)
    {
        using var db = CreateDbContext();
        var targetId = Guid.NewGuid();

        var createHandler = new CreateLandingPageTemplateCommandHandler(db, CurrentUser);
        var created = await createHandler.Handle(new CreateLandingPageTemplateCommand("Only Page", null, scope, targetId), default);

        // Never activated, so the active-template delete guard doesn't apply either.
        var deleteHandler = new DeleteLandingPageTemplateCommandHandler(db, CurrentUser);
        var result = await deleteHandler.Handle(new DeleteLandingPageTemplateCommand(created.Value.Id), default);

        Assert.True(result.IsSuccess);
    }
}
