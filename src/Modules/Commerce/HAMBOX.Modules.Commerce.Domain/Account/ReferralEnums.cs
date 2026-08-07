namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Lifecycle of a referral from redemption to reward. Rejected and Reversed are reserved for fraud
/// review and manual reward reversal (not implemented yet) — no transition currently produces them.
/// </summary>
public enum ReferralStatus
{
    Pending = 0,
    Qualified = 1,
    Rewarded = 2,
    Rejected = 3,
    Expired = 4,
    Reversed = 5,
}

public enum ReferralAuditAction
{
    Redeemed = 0,
    Qualified = 1,
    Rewarded = 2,
    Expired = 3,
    Reversed = 4,

    /// <summary>Reserved for fraud review (not implemented) — no code path sets this yet.</summary>
    Rejected = 5,
}
