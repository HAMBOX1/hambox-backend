namespace HAMBOX.Modules.Commerce.Application.Contracts.Referrals;

public sealed record AdminReferralListItemDto(
    Guid Id,
    string ReferralCode,
    string ReferrerUserId,
    string ReferredEmail,
    string ReferredDisplayName,
    string Status,
    int PointsEarned,
    DateTimeOffset CreatedOnUtc,
    DateTime? QualifiedOnUtc,
    DateTime? RewardedOnUtc,
    DateTime? ExpiresOnUtc);

public sealed record AdminReferralAuditEntryDto(
    Guid Id,
    string Action,
    int? Points,
    string? PerformedByUserId,
    string? Details,
    DateTimeOffset OccurredOnUtc);

public sealed record AdminReferralDetailDto(
    AdminReferralListItemDto Referral,
    IReadOnlyList<AdminReferralAuditEntryDto> AuditTrail);
