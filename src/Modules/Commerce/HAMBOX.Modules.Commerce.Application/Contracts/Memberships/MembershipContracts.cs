namespace HAMBOX.Modules.Commerce.Application.Contracts.Memberships;

public sealed record MembershipBenefitDto(string Type, string Value, string DisplayName, int SortOrder);

public sealed record MembershipPlanListItemDto(
    Guid Id,
    string Name,
    string Slug,
    decimal Price,
    int DurationDays,
    string Status,
    int SortOrder,
    string? BadgeLabel,
    bool IsDefault,
    int BenefitCount,
    int ActiveSubscribers,
    IReadOnlyList<MembershipBenefitDto> Benefits);

public sealed record MembershipPlanDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    int DurationDays,
    string Status,
    int SortOrder,
    string? BadgeLabel,
    string? ThemeKey,
    bool IsDefault,
    IReadOnlyList<MembershipBenefitDto> Benefits,
    IReadOnlyList<Guid> ExclusiveProductIds,
    IReadOnlyList<Guid> UnlockedThemeIds,
    DateTimeOffset CreatedOnUtc);

public sealed record CreateMembershipPlanRequest(
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    int DurationDays,
    int SortOrder,
    string? BadgeLabel,
    string? ThemeKey,
    bool IsDefault,
    IReadOnlyList<MembershipBenefitDto>? Benefits,
    IReadOnlyList<Guid>? ExclusiveProductIds,
    IReadOnlyList<Guid>? UnlockedThemeIds);

public sealed record UpdateMembershipPlanRequest(
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    int DurationDays,
    int SortOrder,
    string? BadgeLabel,
    string? ThemeKey,
    IReadOnlyList<MembershipBenefitDto>? Benefits,
    IReadOnlyList<Guid>? ExclusiveProductIds,
    IReadOnlyList<Guid>? UnlockedThemeIds);

public sealed record AssignMembershipRequest(string UserId, Guid PlanId, bool AutoRenew);

public sealed record BulkAssignMembershipRequest(IReadOnlyList<string> UserIds, Guid PlanId, bool AutoRenew);

public sealed record ChangeMembershipPlanRequest(Guid TargetPlanId);

public sealed record MembershipSubscriptionDto(
    Guid Id,
    string UserId,
    Guid PlanId,
    string PlanName,
    string Status,
    DateTime StartsOnUtc,
    DateTime ExpiresOnUtc,
    bool AutoRenew,
    string? BadgeLabel);

public sealed record MembershipHistoryDto(
    Guid Id,
    Guid PlanId,
    string PlanName,
    string Action,
    Guid? PreviousPlanId,
    string? Notes,
    DateTime OccurredOnUtc);

public sealed record MembershipTransactionDto(
    Guid Id,
    Guid PlanId,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedOnUtc);

public sealed record CurrentMembershipDto(
    MembershipSubscriptionDto? Subscription,
    IReadOnlyList<MembershipBenefitDto> Benefits,
    IReadOnlyList<MembershipPlanListItemDto> AvailablePlans,
    IReadOnlyList<string> FeatureFlags);

public sealed record MembershipStatisticsDto(
    int TotalPlans,
    int ActivePlans,
    int ActiveSubscriptions,
    int ExpiringWithin7Days,
    decimal MonthlyRecurringRevenue);

public sealed record PlanComparisonDto(
    IReadOnlyList<MembershipPlanDetailDto> Plans);

public sealed record MemberListItemDto(
    string UserId,
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    string Status,
    DateTime ExpiresOnUtc);
