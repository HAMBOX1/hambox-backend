namespace HAMBOX.Modules.Communication.Application.Contracts;

public sealed record CommunicationProviderStatusDto(string Channel, string ProviderType, bool IsEnabled);

public sealed record CommunicationDashboardStatsDto(
    int NotificationsToday,
    int EmailsSentToday,
    int FailuresToday,
    int RetryingCount,
    int UnreadNotifications,
    double? AverageDeliverySeconds,
    int QueueSize,
    IReadOnlyList<CommunicationProviderStatusDto> Providers);

public sealed record CommunicationTemplateListItemDto(
    Guid Id,
    string Key,
    string Channel,
    string Category,
    bool HasPublishedVersion,
    int? PublishedVersionNumber,
    bool HasDraft,
    int VersionCount);

public sealed record CommunicationTemplateVersionDto(
    Guid Id,
    int VersionNumber,
    string SubjectEn,
    string? SubjectAr,
    string BodyEn,
    string? BodyAr,
    string? VariablesJson,
    bool IsPublished,
    DateTimeOffset? PublishedOnUtc,
    string? PublishedBy);

public sealed record CommunicationTemplateDetailDto(
    Guid Id,
    string Key,
    string Channel,
    string Category,
    Guid? ActiveVersionId,
    IReadOnlyList<CommunicationTemplateVersionDto> Versions);

public sealed record CreateCommunicationTemplateRequest(string Key, string Channel, string Category);

public sealed record UpdateCommunicationTemplateRequest(
    string SubjectEn,
    string? SubjectAr,
    string BodyEn,
    string? BodyAr,
    string? VariablesJson);

public sealed record CommunicationTemplatePreviewDto(string Subject, string Body);

public sealed record CommunicationMessageListItemDto(
    Guid Id,
    string UserId,
    string Channel,
    string Category,
    string Priority,
    string Status,
    string TemplateKey,
    string Subject,
    int RetryCount,
    string? LastError,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? SentOnUtc);

public sealed record CommunicationMessageDetailDto(
    Guid Id,
    string UserId,
    string Channel,
    string Category,
    string Priority,
    string Status,
    string TemplateKey,
    string Subject,
    string Body,
    int RetryCount,
    string? LastError,
    string? MetadataJson,
    string? CorrelationId,
    string? RelatedEntityType,
    string? RelatedEntityId,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? SentOnUtc,
    DateTimeOffset? FailedOnUtc);

public sealed record CommunicationProviderConfigDto(
    Guid Id,
    string Name,
    string ProviderType,
    string Channel,
    int Priority,
    bool IsEnabled);

public sealed record UpdateCommunicationProviderRequest(bool IsEnabled, int Priority);

public sealed record CommunicationAuditLogEntryDto(
    Guid Id,
    string Action,
    string? ActorUserId,
    string? EntityType,
    string? EntityId,
    string? Details,
    DateTimeOffset CreatedOnUtc);
