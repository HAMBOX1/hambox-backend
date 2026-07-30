using HAMBOX.Modules.Support.Domain.KnowledgeBase;

namespace HAMBOX.Modules.Support.Application.Contracts;

public sealed record KnowledgeCategoryDto(Guid Id, string Name, string Slug, int SortOrder, bool IsActive);

public sealed record KnowledgeArticleSummaryDto(
    Guid Id, string Title, string Slug, Guid CategoryId, string CategoryName,
    KnowledgeArticleStatus Status, KnowledgeArticleVisibility Visibility, int ViewCount, DateTimeOffset? PublishedOnUtc);

public sealed record KnowledgeArticleDetailDto(
    Guid Id, string Title, string Slug, string Body, Guid CategoryId, string CategoryName,
    KnowledgeArticleStatus Status, KnowledgeArticleVisibility Visibility, int ViewCount,
    IReadOnlyList<KnowledgeArticleSummaryDto> RelatedArticles, DateTimeOffset? PublishedOnUtc);
