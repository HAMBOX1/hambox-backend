using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.Modules.Support.Domain.SavedReplies;

namespace HAMBOX.Modules.Support.Application.Services;

internal static class KnowledgeBaseMapper
{
    public static KnowledgeCategoryDto ToDto(KnowledgeCategory category) => new(
        category.Id, category.Name, category.Slug, category.SortOrder, category.IsActive);

    public static KnowledgeArticleSummaryDto ToSummaryDto(KnowledgeArticle article, string categoryName) => new(
        article.Id, article.Title, article.Slug, article.CategoryId, categoryName,
        article.Status, article.Visibility, article.ViewCount, article.PublishedOnUtc);

    public static SavedReplyFolderDto ToDto(SavedReplyFolder folder) => new(folder.Id, folder.Name, folder.SortOrder);

    public static SavedReplyDto ToDto(SavedReply reply) => new(reply.Id, reply.FolderId, reply.Title, reply.Body, reply.UsageCount);
}
