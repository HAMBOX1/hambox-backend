namespace HAMBOX.Modules.Commerce.Domain.Enums;

/// <summary>
/// Membership purchase intent processed through checkout.
/// </summary>
public enum MembershipCheckoutAction
{
    Subscribe = 0,
    Upgrade = 1,
    Downgrade = 2,
    Renew = 3,
}
