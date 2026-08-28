namespace HAMBOX.Modules.Identity.Application.RateLimiting;

/// <summary>
/// Named ASP.NET Core rate limiting policies for authentication endpoints.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Applied to credential-guessing-prone endpoints (login, admin login, Google sign-in).</summary>
    public const string Login = "auth:login";

    /// <summary>Applied to account-action endpoints (register, forgot/reset password, resend verification).</summary>
    public const string AccountActions = "auth:account-actions";

    /// <summary>Applied to the cookie-authenticated refresh/logout endpoints.</summary>
    public const string Refresh = "auth:refresh";
}
