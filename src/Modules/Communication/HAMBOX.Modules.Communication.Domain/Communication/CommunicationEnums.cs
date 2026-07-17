namespace HAMBOX.Modules.Communication.Domain.Communication;

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

public enum CommunicationStatus
{
    Queued = 0,
    Sending = 1,
    Sent = 2,
    Delivered = 3,
    Read = 4,
    Failed = 5,
    Cancelled = 6,
    Retrying = 7,
}

/// <summary>
/// Well-known channel keys resolved by <c>ICommunicationProviderRegistry</c>. A plain string, not a
/// closed enum, mirroring <c>Supplier.ProviderType</c> — adding a future channel (Sms, WhatsApp, ...)
/// means one new provider class plus one new constant here, never a schema change.
/// </summary>
public static class CommunicationChannels
{
    public const string InApp = "InApp";
    public const string Email = "Email";
}

public enum CommunicationAuditAction
{
    TemplateCreated = 0,
    TemplateUpdated = 1,
    TemplatePublished = 2,
    ProviderEnabled = 3,
    ProviderDisabled = 4,
    ProviderPriorityChanged = 5,
    ManualSend = 6,
    NotificationDeleted = 7,
    PreferenceUpdated = 8,
}
