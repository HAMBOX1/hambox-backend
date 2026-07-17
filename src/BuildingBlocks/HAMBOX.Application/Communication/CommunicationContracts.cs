using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Application.Communication;

/// <summary>
/// Notification categories a business module can send. Mirrors the platform brief's "Notification
/// Types" list. Keep in sync with <c>CommunicationCategory</c> (Communication.Domain) — this copy exists
/// so BuildingBlocks (and every module that depends on it) never needs a project reference to the
/// Communication module itself.
/// </summary>
public enum CommunicationCategory
{
    Order = 0,
    Membership = 1,
    Security = 2,
    Promotion = 3,
    System = 4,
    Support = 5,
    Supplier = 6,
    Marketing = 7,
    General = 8,
}

public enum CommunicationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}

/// <summary>
/// A request to notify one user, resolved to zero or more channels (per the user's preferences and the
/// active <c>CommunicationTemplate</c> for <paramref name="TemplateKey"/>) and delivered asynchronously
/// through the background job framework. This is the one thing business modules depend on to notify a
/// user — they never touch email, never touch <c>UserNotification</c>, never block on delivery.
/// </summary>
public sealed record CommunicationRequest(
    string UserId,
    string TemplateKey,
    CommunicationCategory Category,
    IReadOnlyDictionary<string, string> Variables,
    CommunicationPriority Priority = CommunicationPriority.Normal,
    string? CorrelationId = null,
    string? RelatedEntityType = null,
    string? RelatedEntityId = null,
    // Optional deep-link surfaced by the InApp channel (mirrors UserNotification.ActionUrl).
    string? ActionUrl = null,
    IReadOnlyCollection<string>? ChannelOverride = null);

/// <summary>
/// The one contract every business module depends on to send a notification. Implemented in
/// Communication.Infrastructure, registered in DI. Never sends synchronously — it renders the template,
/// checks preferences, records the message, and enqueues delivery via <c>IBackgroundJobScheduler</c>;
/// the caller's handler returns immediately.
/// </summary>
public interface ICommunicationService
{
    Task<Result> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default);
}
