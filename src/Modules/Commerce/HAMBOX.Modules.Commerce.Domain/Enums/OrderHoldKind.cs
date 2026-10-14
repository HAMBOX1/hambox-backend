namespace HAMBOX.Modules.Commerce.Domain.Enums;

/// <summary>Why a paid order is not being fulfilled automatically.</summary>
public enum OrderHoldKind
{
    None = 0,

    /// <summary>Flagged by the system (sensitive product, suspicious activity) — waits for an admin to approve.</summary>
    ManualReview = 1,

    /// <summary>An admin deliberately paused the order.</summary>
    AdminHold = 2,
}
