namespace HAMBOX.Modules.Identity.Domain.Enums;

/// <summary>
/// Represents the status of a user account.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// The account has been created but is not yet activated.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The account is active and fully operational.
    /// </summary>
    Active = 1,

    /// <summary>
    /// The account has been temporarily suspended.
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// The account has been temporarily blocked (see <c>ApplicationUser.BlockExpiresOnUtc</c>).
    /// </summary>
    Blocked = 3,

    /// <summary>
    /// The account has been permanently banned.
    /// </summary>
    Banned = 4,

    /// <summary>
    /// The account has been deleted.
    /// </summary>
    Deleted = 5
}
