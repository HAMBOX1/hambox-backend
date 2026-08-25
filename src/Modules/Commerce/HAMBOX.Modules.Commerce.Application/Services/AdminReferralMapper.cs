using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.Modules.Commerce.Domain.Account;

namespace HAMBOX.Modules.Commerce.Application.Services;

public static class AdminReferralMapper
{
    public static AdminReferralListItemDto ToListItem(ReferralHistoryEntry entry, string referralCode) =>
        new(
            entry.Id,
            referralCode,
            entry.ReferrerUserId,
            entry.ReferredEmail,
            AdminOrderMapper.ResolveCustomerName(entry.ReferredEmail),
            entry.Status.ToString(),
            entry.PointsEarned,
            entry.CreatedOnUtc,
            entry.QualifiedOnUtc,
            entry.RewardedOnUtc,
            entry.ExpiresOnUtc);

    public static AdminReferralAuditEntryDto ToAuditEntry(ReferralAuditLog log) =>
        new(
            log.Id,
            log.Action.ToString(),
            log.Points,
            log.PerformedByUserId,
            log.Details,
            log.OccurredOnUtc);
}
