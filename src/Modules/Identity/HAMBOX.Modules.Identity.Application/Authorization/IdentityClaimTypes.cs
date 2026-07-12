namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Claim types used in JWT access tokens.
/// </summary>
public static class IdentityClaimTypes
{
    public const string SecurityStamp = "security_stamp";
    public const string Role = "role";
    public const string Permission = "permission";
    public const string AuthContext = "auth_context";
    public const string OtpVerified = "otp_verified";
    public const string SessionId = "session_id";
}
