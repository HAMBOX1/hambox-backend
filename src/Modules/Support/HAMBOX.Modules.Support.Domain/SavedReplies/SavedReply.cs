using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.SavedReplies;

/// <summary>
/// A reusable canned reply. <see cref="Body"/> holds <c>{{CustomerName}}</c>/<c>{{OrderNumber}}</c>/
/// <c>{{Product}}</c>-style placeholders substituted at send-time by the same
/// <c>ICommunicationTemplateRenderer</c> already used for Communication templates.
/// </summary>
public sealed class SavedReply : AggregateRoot, IAuditable, ISoftDeletable
{
    private SavedReply()
    {
    }

    private SavedReply(Guid id, Guid? folderId, string title, string body)
        : base(id)
    {
        FolderId = folderId;
        Title = title;
        Body = body;
    }

    public Guid? FolderId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public int UsageCount { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static SavedReply Create(Guid? folderId, string title, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        return new SavedReply(Guid.NewGuid(), folderId, title.Trim(), body);
    }

    public void Update(Guid? folderId, string title, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        FolderId = folderId;
        Title = title.Trim();
        Body = body;
    }

    public void RecordUsage() => UsageCount++;

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }
}
