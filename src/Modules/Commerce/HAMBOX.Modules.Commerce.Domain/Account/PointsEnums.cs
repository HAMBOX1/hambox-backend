namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>What a <see cref="PointsTransaction"/> row records (the Stars source). Stored as an int — append new values only.</summary>
public enum PointsTransactionType
{
    /// <summary>Stars credited for a paid, completed order.</summary>
    Earned = 1,

    /// <summary>Stars taken back because the order they were earned on was cancelled or (partly) refunded.</summary>
    Reversed = 2,

    /// <summary>Stars from a referral reward (referrer or invitee side).</summary>
    ReferralReward = 3,

    /// <summary>Stars spent (Stars Shop, checkout discount). Negative.</summary>
    Redeemed = 4,

    /// <summary>Manual correction by an authorised staff member. Signed.</summary>
    AdminAdjustment = 5,

    /// <summary>Stars that lapsed unused. Negative.</summary>
    Expired = 6,

    /// <summary>Campaign, mission or milestone bonus.</summary>
    Bonus = 7,

    /// <summary>Stars handed back because a redemption failed or was refunded.</summary>
    RedemptionRefund = 8,
}

/// <summary>
/// Lifecycle of one ledger row. Existing rows are <see cref="Available"/>. A credit stays <see cref="Pending"/>
/// until payment is verified, delivery is complete and the hold has passed; a debit is <see cref="Reserved"/>
/// while a redemption is in flight and <see cref="Available"/> (settled) once it succeeds.
/// </summary>
public enum StarsStatus
{
    Available = 0,
    Pending = 1,
    Reserved = 2,
    Cancelled = 3,
}
