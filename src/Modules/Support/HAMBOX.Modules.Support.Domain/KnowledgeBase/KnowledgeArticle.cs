using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.KnowledgeBase;

public sealed class KnowledgeArticle : AggregateRoot, IAuditable, ISoftDeletable
{
    private KnowledgeArticle()
    {
    }

    private KnowledgeArticle(Guid id, Guid categoryId, string title, string slug, string body)
        : base(id)
    {
        CategoryId = categoryId;
        Title = title;
        Slug = slug;
        Body = body;
        Status = KnowledgeArticleStatus.Draft;
        Visibility = KnowledgeArticleVisibility.Public;
    }

    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public KnowledgeArticleStatus Status { get; private set; }
    public KnowledgeArticleVisibility Visibility { get; private set; }
    public int ViewCount { get; private set; }
    public string? RelatedArticleIdsJson { get; private set; }
    public DateTimeOffset? PublishedOnUtc { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static KnowledgeArticle Create(Guid categoryId, string title, string body, KnowledgeArticleVisibility visibility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new KnowledgeArticle(Guid.NewGuid(), categoryId, title.Trim(), Slugify(title), body)
        {
            Visibility = visibility,
        };
    }

    public void Update(Guid categoryId, string title, string body, KnowledgeArticleVisibility visibility, string? relatedArticleIdsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        CategoryId = categoryId;
        Title = title.Trim();
        Slug = Slugify(title);
        Body = body;
        Visibility = visibility;
        RelatedArticleIdsJson = relatedArticleIdsJson;
    }

    public void Publish()
    {
        Status = KnowledgeArticleStatus.Published;
        PublishedOnUtc = DateTimeOffset.UtcNow;
    }

    public void Unpublish()
    {
        Status = KnowledgeArticleStatus.Draft;
    }

    public void RecordView() => ViewCount++;

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }

    private static string Slugify(string title) => title.Trim().ToLowerInvariant().Replace(' ', '-');
}
