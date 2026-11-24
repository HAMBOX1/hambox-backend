using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Loyalty;

/// <summary>Stable identifiers of the six fixed loyalty rungs. Names, thresholds and visibility are admin-managed.</summary>
public static class LoyaltyRankKeys
{
    public const string Normal = "normal";
    public const string Bronze = "bronze";
    public const string Silver = "silver";
    public const string Gold = "gold";
    public const string Platinum = "platinum";
    public const string Diamond = "diamond";

    public static readonly IReadOnlyList<string> All = [Normal, Bronze, Silver, Gold, Platinum, Diamond];
}

/// <summary>
/// One rung of the loyalty ladder (Normal → Diamond). The <see cref="Threshold"/> is measured in the configured
/// criterion's unit (USD spent, or completed orders). ELITE is deliberately not a rank — it is a subscription.
/// </summary>
public sealed class LoyaltyRank : Entity
{
    private LoyaltyRank()
    {
    }

    private LoyaltyRank(Guid id)
        : base(id)
    {
    }

    public string Key { get; private set; } = string.Empty;

    public string NameEn { get; private set; } = string.Empty;

    public string NameAr { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public decimal Threshold { get; private set; }

    public bool IsVisible { get; private set; } = true;

    public string? AccentColor { get; private set; }

    public static LoyaltyRank Create(
        string key, string nameEn, string nameAr, int sortOrder, decimal threshold, string? accentColor)
    {
        var rank = new LoyaltyRank(Guid.NewGuid()) { Key = key };
        rank.Update(nameEn, nameAr, sortOrder, threshold, true, accentColor);
        return rank;
    }

    public void Update(string nameEn, string nameAr, int sortOrder, decimal threshold, bool isVisible, string? accentColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);

        NameEn = nameEn.Trim();
        NameAr = nameAr.Trim();
        SortOrder = sortOrder;
        Threshold = threshold;
        IsVisible = Key == LoyaltyRankKeys.Normal || isVisible;
        AccentColor = string.IsNullOrWhiteSpace(accentColor) ? null : accentColor.Trim();
    }
}
