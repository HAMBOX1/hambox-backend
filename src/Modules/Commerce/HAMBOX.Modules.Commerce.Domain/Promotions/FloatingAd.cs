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

    /// <summary>Optional picture shown at the top of the card (an uploaded file path or an https address).</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>When true the card shows a live countdown to <see cref="EndsOnUtc"/> (which must then be set).</summary>
    public bool ShowCountdown { get; private set; }

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
        int dismissHours,
        string? imageUrl = null,
        bool showCountdown = false)
    {
        var ad = new FloatingAd(Guid.NewGuid());
        ad.Update(titleEn, titleAr, messageEn, messageAr, buttonLabelEn, buttonLabelAr, linkUrl, audience, isActive, startsOnUtc, endsOnUtc, sortOrder, dismissHours, imageUrl, showCountdown);
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
        int dismissHours,
        string? imageUrl = null,
        bool showCountdown = false)
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
        ImageUrl = Clean(imageUrl);
        ShowCountdown = showCountdown && endsOnUtc is not null;
    }

    /// <summary>The card design: "Mini" (small card), "Product" (picture and price) or "Promo" (offer / discount).</summary>
    public string Style { get; private set; } = DefaultStyle;

    /// <summary>Where the card sits on a computer: "BottomLeft" or "BottomRight".</summary>
    public string PositionDesktop { get; private set; } = DefaultPositionDesktop;

    /// <summary>Where the card sits on a phone: "Bottom" (above the bottom bar) or "Top" (under the header).</summary>
    public string PositionMobile { get; private set; } = DefaultPositionMobile;

    /// <summary>Comma-separated page keys the ad appears on (see <see cref="PageKeys"/>); empty = every storefront page.</summary>
    public string? Pages { get; private set; }

    /// <summary>The price shown on a Product card (free text, e.g. "$19.99").</summary>
    public string? PriceText { get; private set; }

    /// <summary>The old price shown struck through next to <see cref="PriceText"/>.</summary>
    public string? OldPriceText { get; private set; }

    public const string DefaultStyle = "Mini";
    public const string DefaultPositionDesktop = "BottomLeft";
    public const string DefaultPositionMobile = "Bottom";

    public static readonly IReadOnlyList<string> Styles = ["Mini", "Product", "Promo"];
    public static readonly IReadOnlyList<string> DesktopPositions = ["BottomLeft", "BottomRight"];
    public static readonly IReadOnlyList<string> MobilePositions = ["Bottom", "Top"];

    /// <summary>The storefront pages an ad can be limited to.</summary>
    public static readonly IReadOnlyList<string> PageKeys = ["home", "products", "product", "category"];

    public IReadOnlyList<string> GetPages() =>
        string.IsNullOrWhiteSpace(Pages) ? [] : Pages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Sets how the card looks and where it appears. Unknown values fall back to the defaults.</summary>
    public void SetPresentation(
        string? style,
        string? positionDesktop,
        string? positionMobile,
        IEnumerable<string>? pages,
        string? priceText,
        string? oldPriceText)
    {
        Style = Pick(style, Styles, DefaultStyle);
        PositionDesktop = Pick(positionDesktop, DesktopPositions, DefaultPositionDesktop);
        PositionMobile = Pick(positionMobile, MobilePositions, DefaultPositionMobile);
        var chosen = (pages ?? []).Select(p => p.Trim().ToLowerInvariant()).Where(PageKeys.Contains).Distinct().ToList();
        Pages = chosen.Count == 0 || chosen.Count == PageKeys.Count ? null : string.Join(',', chosen);
        PriceText = Clean(priceText);
        OldPriceText = Clean(oldPriceText);
    }

    private static string Pick(string? value, IReadOnlyList<string> allowed, string fallback) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? fallback;

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
