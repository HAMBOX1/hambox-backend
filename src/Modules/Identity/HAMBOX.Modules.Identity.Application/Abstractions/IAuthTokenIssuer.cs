using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Issues JWT and refresh tokens with auth-context claims and session tracking.
/// </summary>
public interface IAuthTokenIssuer
{
    /// <summary>
    /// Creates access and refresh tokens for an authenticated user.
    /// </summary>
    Task<Result<AuthTokenResponse>> IssueAsync(
        ApplicationUser user,
        string authContext,
        bool otpVerified,
        string ipAddress,
        string userAgent,
        bool rememberMe = false,
        LoginContext? context = null,
        CancellationToken cancellationToken = default);
}
