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
    /// The account has been permanently blocked.
    /// </summary>
    Blocked = 3
}
