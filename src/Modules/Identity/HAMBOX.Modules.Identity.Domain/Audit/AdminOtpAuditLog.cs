using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Audit;

/// <summary>
/// Audit log entry for admin OTP lifecycle events.
/// </summary>
public sealed class AdminOtpAuditLog : Entity
{
    public const string ActionGenerated = "Generated";
    public const string ActionSent = "Sent";
    public const string ActionVerified = "Verified";
    public const string ActionFailed = "Failed";
    public const string ActionLocked = "Locked";
    public const string ActionResent = "Resent";
    public const string ActionBypassed = "Bypassed";

    private AdminOtpAuditLog()
    {
    }

    private AdminOtpAuditLog(
        Guid id,
        Guid? userId,
        Guid? challengeId,
        string action,
        string ipAddress,
        string? details,
        DateTimeOffset occurredOnUtc)
        : base(id)
    {
        UserId = userId;
        ChallengeId = challengeId;
        Action = action;
        IpAddress = ipAddress;
        Details = details;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid? UserId { get; private set; }

    public Guid? ChallengeId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string IpAddress { get; private set; } = string.Empty;

    public string? Details { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static AdminOtpAuditLog Record(
        string action,
        string ipAddress,
        Guid? userId = null,
        Guid? challengeId = null,
        string? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        return new AdminOtpAuditLog(
            Guid.NewGuid(),
            userId,
            challengeId,
            action,
            ipAddress,
            details,
            DateTimeOffset.UtcNow);
    }
}
