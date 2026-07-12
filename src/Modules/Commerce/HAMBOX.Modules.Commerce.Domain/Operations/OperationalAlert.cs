namespace HAMBOX.Modules.Commerce.Domain.Operations;

public enum OperationalAlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

public sealed class OperationalAlert
{
    private OperationalAlert()
    {
    }

    private OperationalAlert(
        Guid id,
        string code,
        string title,
        string message,
        OperationalAlertSeverity severity,
        string? relatedEntityType,
        string? relatedEntityId)
    {
        Id = id;
        Code = code;
        Title = title;
        Message = message;
        Severity = severity;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        CreatedOnUtc = DateTimeOffset.UtcNow;
        IsAcknowledged = false;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public OperationalAlertSeverity Severity { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public string? RelatedEntityId { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public bool IsAcknowledged { get; private set; }
    public DateTimeOffset? AcknowledgedOnUtc { get; private set; }
    public string? AcknowledgedByUserId { get; private set; }

    public static OperationalAlert Create(
        string code,
        string title,
        string message,
        OperationalAlertSeverity severity,
        string? relatedEntityType = null,
        string? relatedEntityId = null) =>
        new(Guid.NewGuid(), code, title, message, severity, relatedEntityType, relatedEntityId);

    public void Acknowledge(string? userId)
    {
        IsAcknowledged = true;
        AcknowledgedOnUtc = DateTimeOffset.UtcNow;
        AcknowledgedByUserId = userId;
    }
}
