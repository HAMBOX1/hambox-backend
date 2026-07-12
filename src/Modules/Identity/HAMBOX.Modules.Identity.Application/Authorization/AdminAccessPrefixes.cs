namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Permission name prefixes that grant access to the admin portal.
/// </summary>
public static class AdminAccessPrefixes
{
    public static readonly string[] All =
    [
        "Dashboard.",
        "Catalog.",
        "Roles.",
        "Orders.",
        "Customers.",
        "Users.",
        "Permissions.",
        "Memberships.",
        "Promotions.",
        "Coupons.",
        "Themes.",
        "Reviews.",
        "Notifications.",
        "Referral.",
        "Reports.",
        "Localization.",
        "Settings.",
        "Support.",
        "Media.",
        "AuditLogs.",
        "Operations.",
        "Analytics.",
    ];

    public static bool IsAdminAccessPermission(string permission) =>
        All.Any(prefix => permission.StartsWith(prefix, StringComparison.Ordinal));
}
