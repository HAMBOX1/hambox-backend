using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Audit trail for referral lifecycle events (redemption, qualification, reward, expiry).
/// </summary>
public sealed class ReferralAuditLog : Entity
{
    private ReferralAuditLog()
    {
    }

    private ReferralAuditLog(
        Guid id,
        Guid referralHistoryEntryId,
        string referrerUserId,
        ReferralAuditAction action,
        int? points,
        string? performedByUserId,
        string? details)
        : base(id)
    {
        ReferralHistoryEntryId = referralHistoryEntryId;
        ReferrerUserId = referrerUserId;
        Action = action;
        Points = points;
        PerformedByUserId = performedByUserId;
        Details = details;
        OccurredOnUtc = DateTime.UtcNow;
    }

    public Guid ReferralHistoryEntryId { get; private set; }
    public string ReferrerUserId { get; private set; } = string.Empty;
    public ReferralAuditAction Action { get; private set; }
    public int? Points { get; private set; }
    public string? PerformedByUserId { get; private set; }
    public string? Details { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }

    public static ReferralAuditLog Create(
        Guid referralHistoryEntryId,
        string referrerUserId,
        ReferralAuditAction action,
        int? points,
        string? performedByUserId,
        string? details = null) =>
        new(Guid.NewGuid(), referralHistoryEntryId, referrerUserId, action, points, performedByUserId, details);
}
