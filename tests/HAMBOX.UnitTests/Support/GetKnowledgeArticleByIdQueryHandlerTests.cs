using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticleById;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.UnitTests.Support.TestDoubles;

namespace HAMBOX.UnitTests.Support;

/// <summary>
/// The public article reader must answer for a draft or an Internal (agent-only) article exactly like a missing one:
/// the by-id endpoint used to return both to anonymous callers even though the list endpoint filtered them out.
/// </summary>
public sealed class GetKnowledgeArticleByIdQueryHandlerTests
{
    private static async Task<(GetKnowledgeArticleByIdQueryHandler Handler, KnowledgeArticle Published, KnowledgeArticle Internal, KnowledgeArticle Draft)> SeedAsync()
    {
        var (supportDb, _, _) = SupportTestDbContextFactory.Create();
        var categoryId = Guid.NewGuid();

        var published = KnowledgeArticle.Create(categoryId, "Public how-to", "<p>hi</p>", KnowledgeArticleVisibility.Public);
        published.Publish();
        var internalArticle = KnowledgeArticle.Create(categoryId, "Agent playbook", "<p>secret</p>", KnowledgeArticleVisibility.Internal);
        internalArticle.Publish();
        var draft = KnowledgeArticle.Create(categoryId, "Not ready", "<p>wip</p>", KnowledgeArticleVisibility.Public);

        supportDb.KnowledgeArticles.AddRange(published, internalArticle, draft);
        await supportDb.SaveChangesAsync(CancellationToken.None);

        return (new GetKnowledgeArticleByIdQueryHandler(supportDb), published, internalArticle, draft);
    }

    [Fact]
    public async Task PublicReader_CanOpenAPublishedPublicArticle()
    {
        var (handler, published, _, _) = await SeedAsync();

        var result = await handler.Handle(new GetKnowledgeArticleByIdQuery(published.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PublicReader_CannotOpenAnInternalArticle()
    {
        var (handler, _, internalArticle, _) = await SeedAsync();

        var result = await handler.Handle(new GetKnowledgeArticleByIdQuery(internalArticle.Id, true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SupportErrors.KnowledgeArticleNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task PublicReader_CannotOpenADraft()
    {
        var (handler, _, _, draft) = await SeedAsync();

        var result = await handler.Handle(new GetKnowledgeArticleByIdQuery(draft.Id, true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SupportErrors.KnowledgeArticleNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task AdminReader_CanOpenInternalArticlesAndDrafts()
    {
        var (handler, _, internalArticle, draft) = await SeedAsync();

        Assert.True((await handler.Handle(new GetKnowledgeArticleByIdQuery(internalArticle.Id, false), CancellationToken.None)).IsSuccess);
        Assert.True((await handler.Handle(new GetKnowledgeArticleByIdQuery(draft.Id, false), CancellationToken.None)).IsSuccess);
    }
}
