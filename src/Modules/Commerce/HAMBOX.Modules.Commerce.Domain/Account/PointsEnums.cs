namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>What a <see cref="PointsTransaction"/> row records. Stored as an int — append new values only.</summary>
public enum PointsTransactionType
{
    /// <summary>Points credited for a paid, completed order.</summary>
    Earned = 1,

    /// <summary>Points taken back because the order they were earned on was cancelled or (partly) refunded.</summary>
    Reversed = 2,
}
