using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Missions;

public enum MissionKind
{
    /// <summary>Complete this many paid, delivered product orders.</summary>
    CompletedOrders = 0,

    /// <summary>Spend this much (USD) across paid, delivered product orders.</summary>
    SpendUsd = 1,
}

/// <summary>
/// An achievement customers earn once: reach a number of completed orders or a spend total, and receive Stars (and optionally a
/// badge). Only delivered orders count, so a late or cancelled order never completes a mission early.
/// </summary>
public sealed class Mission : AggregateRoot
{
    private Mission()
    {
    }

    private Mission(Guid id)
        : base(id)
    {
    }

    public string NameEn { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public string? DescriptionEn { get; private set; }

    public string? DescriptionAr { get; private set; }

    public MissionKind Kind { get; private set; }

    public decimal Target { get; private set; }

    public int RewardStars { get; private set; }

    /// <summary>A short badge identifier shown on the customer's profile once earned (e.g. "founding-customer").</summary>
    public string? BadgeKey { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? StartsOnUtc { get; private set; }

    public DateTimeOffset? EndsOnUtc { get; private set; }

    public int SortOrder { get; private set; }

    public static Mission Create(
        string nameEn, string nameAr, string? descriptionEn, string? descriptionAr, MissionKind kind, decimal target, int rewardStars,
        string? badgeKey, bool isActive, DateTimeOffset? startsOnUtc, DateTimeOffset? endsOnUtc, int sortOrder)
    {
        var mission = new Mission(Guid.NewGuid());
        mission.Apply(nameEn, nameAr, descriptionEn, descriptionAr, kind, target, rewardStars, badgeKey, isActive, startsOnUtc, endsOnUtc, sortOrder);
        return mission;
    }

    public void Apply(
        string nameEn, string nameAr, string? descriptionEn, string? descriptionAr, MissionKind kind, decimal target, int rewardStars,
        string? badgeKey, bool isActive, DateTimeOffset? startsOnUtc, DateTimeOffset? endsOnUtc, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(target, 0m);
        ArgumentOutOfRangeException.ThrowIfNegative(rewardStars);
        if (startsOnUtc is { } s && endsOnUtc is { } e && e <= s)
        {
            throw new ArgumentException("The end must be after the start.", nameof(endsOnUtc));
        }

        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        DescriptionEn = string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim();
        DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? null : descriptionAr.Trim();
        Kind = kind;
        Target = target;
        RewardStars = rewardStars;
        BadgeKey = string.IsNullOrWhiteSpace(badgeKey) ? null : badgeKey.Trim().ToLowerInvariant();
        IsActive = isActive;
        StartsOnUtc = startsOnUtc;
        EndsOnUtc = endsOnUtc;
        SortOrder = sortOrder;
    }

    public bool IsLiveAt(DateTimeOffset now) =>
        IsActive && (StartsOnUtc is null || StartsOnUtc <= now) && (EndsOnUtc is null || EndsOnUtc > now);
}

/// <summary>That a customer earned a mission. Unique per (mission, customer): a mission rewards once.</summary>
public sealed class MissionCompletion : Entity
{
    private MissionCompletion()
    {
    }

    private MissionCompletion(Guid id)
        : base(id)
    {
    }

    public Guid MissionId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public int StarsAwarded { get; private set; }

    public DateTimeOffset CompletedOnUtc { get; private set; }

    public static MissionCompletion Create(Guid missionId, string userId, int starsAwarded) =>
        new(Guid.NewGuid()) { MissionId = missionId, UserId = userId, StarsAwarded = starsAwarded, CompletedOnUtc = DateTimeOffset.UtcNow };
}
