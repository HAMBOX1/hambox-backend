namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// JWT auth context values separating storefront and admin portal sessions.
/// </summary>
public static class AuthContextTypes
{
    public const string Customer = "customer";
    public const string Admin = "admin";
}
