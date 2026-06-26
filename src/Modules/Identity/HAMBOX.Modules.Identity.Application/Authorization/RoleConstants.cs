namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Contains all well-known role names defined in the system.
/// </summary>
public static class RoleConstants
{
    /// <summary>
    /// Role with complete system administration capabilities.
    /// </summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>
    /// Role for general administrative tasks.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Role for managing catalog items and publishing content.
    /// </summary>
    public const string ContentManager = "ContentManager";

    /// <summary>
    /// Role for handling customer support.
    /// </summary>
    public const string SupportAgent = "SupportAgent";

    /// <summary>
    /// Role representing default customer accounts.
    /// </summary>
    public const string Customer = "Customer";
}
