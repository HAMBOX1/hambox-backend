using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Cosmetics;

public enum CosmeticKind
{
    /// <summary>A frame around the customer's avatar / profile card.</summary>
    Frame = 0,

    /// <summary>A badge shown beside the customer's name.</summary>
    Badge = 1,

    /// <summary>A progress animation style (the CSS class is the cosmetic's key).</summary>
    ProgressAnimation = 2,
}

public enum CosmeticUnlock
{
    /// <summary>Everyone can use it.</summary>
    Free = 0,

    /// <summary>Customers at or above a loyalty rank.</summary>
    Rank = 1,

    /// <summary>Customers who completed a mission or milestone.</summary>
    Mission = 2,

    /// <summary>Active ELITE members, for as long as the subscription runs.</summary>
    Elite = 3,

    /// <summary>Bought once with Stars and then kept.</summary>
    Stars = 4,
}

/// <summary>An admin-managed profile decoration (frame, badge or progress animation) and what unlocks it.</summary>
public sealed class ProfileCosmetic : AggregateRoot
{
    private ProfileCosmetic()
    {
    }

    private ProfileCosmetic(Guid id)
        : base(id)
    {
    }

    public CosmeticKind Kind { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    /// <summary>An uploaded image or https address; optional because a badge can be pure colour.</summary>
    public string? ImageUrl { get; private set; }

    public string? AccentColor { get; private set; }

    public CosmeticUnlock Unlock { get; private set; }

    public string? UnlockRankKey { get; private set; }

    public Guid? UnlockMissionId { get; private set; }

    public int? StarsPrice { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public static ProfileCosmetic Create(
        CosmeticKind kind, string key, string nameEn, string nameAr, string? imageUrl, string? accentColor, CosmeticUnlock unlock,
        string? unlockRankKey, Guid? unlockMissionId, int? starsPrice, bool isActive, int sortOrder)
    {
        var cosmetic = new ProfileCosmetic(Guid.NewGuid());
        cosmetic.Apply(kind, key, nameEn, nameAr, imageUrl, accentColor, unlock, unlockRankKey, unlockMissionId, starsPrice, isActive, sortOrder);
        return cosmetic;
    }

    public void Apply(
        CosmeticKind kind, string key, string nameEn, string nameAr, string? imageUrl, string? accentColor, CosmeticUnlock unlock,
        string? unlockRankKey, Guid? unlockMissionId, int? starsPrice, bool isActive, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        if ((unlock == CosmeticUnlock.Rank && string.IsNullOrWhiteSpace(unlockRankKey))
            || (unlock == CosmeticUnlock.Mission && unlockMissionId is null)
            || (unlock == CosmeticUnlock.Stars && starsPrice is not > 0))
        {
            throw new ArgumentException("The unlock rule is missing its rank, mission or Stars price.");
        }

        Kind = kind;
        Key = key.Trim().ToLowerInvariant();
        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        AccentColor = string.IsNullOrWhiteSpace(accentColor) ? null : accentColor.Trim();
        Unlock = unlock;
        UnlockRankKey = unlock == CosmeticUnlock.Rank ? unlockRankKey!.Trim().ToLowerInvariant() : null;
        UnlockMissionId = unlock == CosmeticUnlock.Mission ? unlockMissionId : null;
        StarsPrice = unlock == CosmeticUnlock.Stars ? starsPrice : null;
        IsActive = isActive;
        SortOrder = sortOrder;
    }
}

/// <summary>A cosmetic a customer bought with Stars (the only unlock that is kept rather than recomputed).</summary>
public sealed class CustomerCosmetic : Entity
{
    private CustomerCosmetic()
    {
    }

    private CustomerCosmetic(Guid id)
        : base(id)
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public Guid CosmeticId { get; private set; }

    public DateTimeOffset AcquiredOnUtc { get; private set; }

    public static CustomerCosmetic Create(string userId, Guid cosmeticId) =>
        new(Guid.NewGuid()) { UserId = userId, CosmeticId = cosmeticId, AcquiredOnUtc = DateTimeOffset.UtcNow };
}

/// <summary>What the customer currently wears. A choice that is no longer unlocked is simply not shown.</summary>
public sealed class CustomerProfileStyle : Entity
{
    private CustomerProfileStyle()
    {
    }

    private CustomerProfileStyle(Guid id)
        : base(id)
    {
    }

    public string UserId { get; private set; } = string.Empty;

    public Guid? FrameId { get; private set; }

    public Guid? BadgeId { get; private set; }

    public Guid? AnimationId { get; private set; }

    public static CustomerProfileStyle Create(string userId) => new(Guid.NewGuid()) { UserId = userId };

    public void Set(Guid? frameId, Guid? badgeId, Guid? animationId)
    {
        FrameId = frameId;
        BadgeId = badgeId;
        AnimationId = animationId;
    }
}
