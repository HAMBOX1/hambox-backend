using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

/// <summary>Who a floating ad is shown to.</summary>
public enum FloatingAdAudience
{
    /// <summary>Every visitor.</summary>
    Everyone = 0,

    /// <summary>Visitors who are not signed in.</summary>
    Guests = 1,

    /// <summary>Any signed-in customer.</summary>
    SignedIn = 2,

    /// <summary>Signed-in customers without an active membership (e.g. "get Premium for 20% off").</summary>
    WithoutMembership = 3,

    /// <summary>Signed-in customers with an active membership.</summary>
    WithMembership = 4,
}

/// <summary>
/// A small card that floats over the storefront while customers browse (an offer, a membership discount, ...), with an
/// optional button. The audience decides who sees it, so e.g. a membership offer never shows to people who already
/// bought the membership.
/// </summary>
public sealed class FloatingAd : Entity
{
    private FloatingAd()
    {
    }

    private FloatingAd(Guid id)
        : base(id)
    {
    }

    public string TitleEn { get; private set; } = string.Empty;

    public string? TitleAr { get; private set; }

    public string? MessageEn { get; private set; }

    public string? MessageAr { get; private set; }

    public string? ButtonLabelEn { get; private set; }

    public string? ButtonLabelAr { get; private set; }

    /// <summary>Where the button goes: an in-app path (<c>/memberships</c>) or a full https URL.</summary>
    public string? LinkUrl { get; private set; }

    public FloatingAdAudience Audience { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? StartsOnUtc { get; private set; }

    public DateTimeOffset? EndsOnUtc { get; private set; }

    /// <summary>Lower numbers are shown first when several ads match the same visitor.</summary>
    public int SortOrder { get; private set; }

    /// <summary>After a visitor closes the ad it stays hidden for this many hours.</summary>
    public int DismissHours { get; private set; } = 24;

    public static FloatingAd Create(
        string titleEn,
        string? titleAr,
        string? messageEn,
        string? messageAr,
        string? buttonLabelEn,
        string? buttonLabelAr,
        string? linkUrl,
        FloatingAdAudience audience,
        bool isActive,
        DateTimeOffset? startsOnUtc,
        DateTimeOffset? endsOnUtc,
        int sortOrder,
        int dismissHours)
    {
        var ad = new FloatingAd(Guid.NewGuid());
        ad.Update(titleEn, titleAr, messageEn, messageAr, buttonLabelEn, buttonLabelAr, linkUrl, audience, isActive, startsOnUtc, endsOnUtc, sortOrder, dismissHours);
        return ad;
    }

    public void Update(
        string titleEn,
        string? titleAr,
        string? messageEn,
        string? messageAr,
        string? buttonLabelEn,
        string? buttonLabelAr,
        string? linkUrl,
        FloatingAdAudience audience,
        bool isActive,
        DateTimeOffset? startsOnUtc,
        DateTimeOffset? endsOnUtc,
        int sortOrder,
        int dismissHours)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleEn);

        TitleEn = titleEn.Trim();
        TitleAr = Clean(titleAr);
        MessageEn = Clean(messageEn);
        MessageAr = Clean(messageAr);
        ButtonLabelEn = Clean(buttonLabelEn);
        ButtonLabelAr = Clean(buttonLabelAr);
        LinkUrl = Clean(linkUrl);
        Audience = audience;
        IsActive = isActive;
        StartsOnUtc = startsOnUtc;
        EndsOnUtc = endsOnUtc;
        SortOrder = sortOrder;
        DismissHours = Math.Clamp(dismissHours, 1, 24 * 30);
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
