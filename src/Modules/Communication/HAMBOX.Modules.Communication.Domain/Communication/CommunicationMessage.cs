using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Communication.Domain.Communication;

/// <summary>
/// A single channel delivery of a notification to a single user. Covers both the "Notification" and
/// "Recipient" concepts from the platform brief — every message here has exactly one recipient and one
/// channel; there is no broadcast/fan-out use case in HAMBOX today, so a separate Recipient table would
/// be speculative (split it out if that need appears). Subject/Body are rendered and denormalized at
/// creation time so a later template edit never retroactively changes delivery history.
/// </summary>
public sealed class CommunicationMessage : AggregateRoot
{
    private CommunicationMessage()
    {
    }

    private CommunicationMessage(
        Guid id,
        string userId,
        string channel,
        CommunicationCategory category,
        CommunicationPriority priority,
        string templateKey,
        string subject,
        string body,
        string? correlationId,
        string? relatedEntityType,
        string? relatedEntityId,
        string? metadataJson)
        : base(id)
    {
        UserId = userId;
        Channel = channel;
        Category = category;
        Priority = priority;
        TemplateKey = templateKey;
        Subject = subject;
        Body = body;
        CorrelationId = correlationId;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        MetadataJson = metadataJson;
        Status = CommunicationStatus.Queued;
    }

    public string UserId { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public CommunicationCategory Category { get; private set; }
    public CommunicationPriority Priority { get; private set; }
    public CommunicationStatus Status { get; private set; }
    public string TemplateKey { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }
    public string? MetadataJson { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public string? RelatedEntityId { get; private set; }
    public DateTimeOffset? SentOnUtc { get; private set; }
    public DateTimeOffset? FailedOnUtc { get; private set; }

    public static CommunicationMessage Create(
        string userId,
        string channel,
        CommunicationCategory category,
        CommunicationPriority priority,
        string templateKey,
        string subject,
        string body,
        string? correlationId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        string? metadataJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new CommunicationMessage(
            Guid.NewGuid(),
            userId,
            channel,
            category,
            priority,
            templateKey,
            subject,
            body,
            correlationId,
            relatedEntityType,
            relatedEntityId,
            metadataJson);
    }

    public void MarkSending() => Status = CommunicationStatus.Sending;

    public void MarkSent()
    {
        Status = CommunicationStatus.Sent;
        SentOnUtc = DateTimeOffset.UtcNow;
    }

    public void MarkDelivered()
    {
        Status = CommunicationStatus.Delivered;
        SentOnUtc ??= DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = CommunicationStatus.Failed;
        LastError = error;
        FailedOnUtc = DateTimeOffset.UtcNow;
    }

    public void MarkRetrying(string error)
    {
        Status = CommunicationStatus.Retrying;
        LastError = error;
        RetryCount++;
    }

    public void Cancel() => Status = CommunicationStatus.Cancelled;
}
