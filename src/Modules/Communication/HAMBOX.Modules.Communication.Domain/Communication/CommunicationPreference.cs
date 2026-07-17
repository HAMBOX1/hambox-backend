using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Communication.Domain.Communication;

/// <summary>
/// Per-user, per-category channel opt-in/out. Lazily created on first write; reads fall back to
/// all-enabled defaults when no row exists yet (same "missing row = defaults" philosophy as Platform
/// Settings) — no backfill migration needed for existing users.
/// </summary>
public sealed class CommunicationPreference : Entity
{
    private CommunicationPreference()
    {
    }

    private CommunicationPreference(Guid id, string userId)
        : base(id)
    {
        UserId = userId;
        EmailEnabled = true;
        InAppEnabled = true;
        MarketingEnabled = true;
        SecurityEnabled = true;
        OrderEnabled = true;
        MembershipEnabled = true;
        SupportEnabled = true;
        PromotionEnabled = true;
        GeneralEnabled = true;
    }

    public string UserId { get; private set; } = string.Empty;
    public bool EmailEnabled { get; private set; }
    public bool InAppEnabled { get; private set; }
    public bool MarketingEnabled { get; private set; }
    public bool SecurityEnabled { get; private set; }
    public bool OrderEnabled { get; private set; }
    public bool MembershipEnabled { get; private set; }
    public bool SupportEnabled { get; private set; }
    public bool PromotionEnabled { get; private set; }
    public bool GeneralEnabled { get; private set; }

    public static CommunicationPreference CreateDefault(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new CommunicationPreference(Guid.NewGuid(), userId);
    }

    public void Update(
        bool emailEnabled,
        bool inAppEnabled,
        bool marketingEnabled,
        bool securityEnabled,
        bool orderEnabled,
        bool membershipEnabled,
        bool supportEnabled,
        bool promotionEnabled,
        bool generalEnabled)
    {
        EmailEnabled = emailEnabled;
        InAppEnabled = inAppEnabled;
        MarketingEnabled = marketingEnabled;
        SecurityEnabled = securityEnabled;
        OrderEnabled = orderEnabled;
        MembershipEnabled = membershipEnabled;
        SupportEnabled = supportEnabled;
        PromotionEnabled = promotionEnabled;
        GeneralEnabled = generalEnabled;
    }

    /// <summary>Security notifications are never suppressible — the toggle is ignored for that category.</summary>
    public bool IsChannelEnabledFor(string channel, CommunicationCategory category)
    {
        if (category == CommunicationCategory.Security)
        {
            return true;
        }

        var channelEnabled = channel switch
        {
            CommunicationChannels.Email => EmailEnabled,
            CommunicationChannels.InApp => InAppEnabled,
            _ => true,
        };

        var categoryEnabled = category switch
        {
            CommunicationCategory.Order => OrderEnabled,
            CommunicationCategory.Membership => MembershipEnabled,
            CommunicationCategory.Support => SupportEnabled,
            CommunicationCategory.Promotion => PromotionEnabled,
            CommunicationCategory.Marketing => MarketingEnabled,
            _ => GeneralEnabled,
        };

        return channelEnabled && categoryEnabled;
    }
}
