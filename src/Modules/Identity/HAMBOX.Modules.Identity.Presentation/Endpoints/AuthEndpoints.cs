using HAMBOX.Infrastructure.Localization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Features.AdminLogin;
using HAMBOX.Modules.Identity.Application.Features.ChangePassword;
using HAMBOX.Modules.Identity.Application.Features.ForgotPassword;
using HAMBOX.Modules.Identity.Application.Features.GetMe;
using HAMBOX.Modules.Identity.Application.Features.GoogleLogin;
using HAMBOX.Modules.Identity.Application.Features.Login;
using HAMBOX.Modules.Identity.Application.Features.Logout;
using HAMBOX.Modules.Identity.Application.Features.MaintenanceBypass;
using HAMBOX.Modules.Identity.Application.Features.RefreshToken;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Application.Features.ResendVerification;
using HAMBOX.Modules.Identity.Application.Features.ResetPassword;
using HAMBOX.Modules.Identity.Application.Features.Sessions;
using HAMBOX.Modules.Identity.Application.Features.UpdateProfile;
using HAMBOX.Modules.Identity.Application.Features.VerifyEmail;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Application.RateLimiting;
using HAMBOX.Modules.Identity.Presentation.Authentication;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Presentation.Endpoints;

/// <summary>
/// Defines authentication endpoints.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication endpoints to the route builder.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    /// <returns>The route group builder.</returns>
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("api/auth")
            .WithTags("Authentication");

        group.MapPost("register", async (
            [FromBody] RegisterRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
            var language = httpContext.Request.Headers["Accept-Language"].ToString() is { Length: > 0 } acceptLanguage
                ? acceptLanguage.Split(',')[0].Split(';')[0].Trim()
                : "en";

            var command = new RegisterCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                ipAddress,
                userAgent,
                language,
                request.ReferralCode,
                request.TurnstileToken);

            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireRateLimiting(RateLimitPolicies.AccountActions);

        group.MapPost("login", async (
            [FromBody] LoginRequest request,
            HttpContext httpContext,
            ISender sender,
            IOptions<RefreshCookieSettings> cookieSettingsOptions,
            IHostEnvironment environment,
            ITokenGenerator tokenGenerator,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
            var geoLocation = GetGeoLocation(httpContext);

            var command = new LoginCommand(
                request.Email,
                request.Password,
                ipAddress,
                userAgent,
                geoLocation?.CountryCode,
                geoLocation?.City,
                request.RememberMe);

            var result = await sender.Send(command, ct);
            return HandleTokenResult(httpContext, result, cookieSettingsOptions.Value, environment, tokenGenerator);
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("google", async (
            [FromBody] GoogleLoginRequest request,
            HttpContext httpContext,
            ISender sender,
            IOptions<RefreshCookieSettings> cookieSettingsOptions,
            IHostEnvironment environment,
            ITokenGenerator tokenGenerator,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
            var geoLocation = GetGeoLocation(httpContext);

            var command = new GoogleLoginCommand(
                request.IdToken,
                ipAddress,
                userAgent,
                geoLocation?.CountryCode,
                geoLocation?.City);

            var result = await sender.Send(command, ct);
            return HandleTokenResult(httpContext, result, cookieSettingsOptions.Value, environment, tokenGenerator);
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("admin/login", async (
            [FromBody] LoginRequest request,
            HttpContext httpContext,
            ISender sender,
            IOptions<RefreshCookieSettings> cookieSettingsOptions,
            IHostEnvironment environment,
            ITokenGenerator tokenGenerator,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
            var geoLocation = GetGeoLocation(httpContext);

            var command = new AdminLoginCommand(
                request.Email,
                request.Password,
                ipAddress,
                userAgent,
                geoLocation?.CountryCode,
                geoLocation?.City);

            var result = await sender.Send(command, ct);
            if (!result.IsSuccess)
            {
                return LocalizedEndpointResults.FromResult(httpContext, result);
            }

            // Token is only populated when Admin OTP is disabled via Platform Settings — the caller
            // is already fully authenticated and the cookie must be issued now, same as a normal login.
            var challenge = result.Value;
            AccessTokenResponseDto? tokenDto = null;
            if (challenge.Token is not null)
            {
                WriteAuthCookies(httpContext, challenge.Token, cookieSettingsOptions.Value, environment, tokenGenerator);
                tokenDto = ToAccessTokenResponse(challenge.Token);
            }

            return Results.Ok(new AdminLoginChallengeResponseDto(
                challenge.ChallengeId, challenge.ExpiresAt, challenge.ResendAvailableAt, challenge.MaskedEmail, tokenDto));
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("admin/verify-otp", async (
            [FromBody] VerifyAdminOtpRequest request,
            HttpContext httpContext,
            ISender sender,
            IOptions<RefreshCookieSettings> cookieSettingsOptions,
            IHostEnvironment environment,
            ITokenGenerator tokenGenerator,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";

            var command = new VerifyAdminOtpCommand(
                request.ChallengeId,
                request.Code,
                ipAddress,
                userAgent);

            var result = await sender.Send(command, ct);
            return HandleTokenResult(httpContext, result, cookieSettingsOptions.Value, environment, tokenGenerator);
        });

        group.MapPost("admin/resend-otp", async (
            [FromBody] ResendAdminOtpRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var command = new ResendAdminOtpCommand(request.ChallengeId, ipAddress);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

        group.MapPost("maintenance-bypass", async (
            [FromBody] MaintenanceBypassRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new VerifyMaintenanceBypassCommand(request.Password);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("refresh", async (
            HttpContext httpContext,
            ISender sender,
            IOptions<RefreshCookieSettings> cookieSettingsOptions,
            IHostEnvironment environment,
            ITokenGenerator tokenGenerator,
            CancellationToken ct) =>
        {
            var cookieSettings = cookieSettingsOptions.Value;

            if (!httpContext.Request.Cookies.TryGetValue(cookieSettings.CookieName, out var refreshTokenPlaintext)
                || string.IsNullOrWhiteSpace(refreshTokenPlaintext))
            {
                return Results.Unauthorized();
            }

            if (!CsrfCookieWriter.ValidateCsrfToken(httpContext))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var command = new RefreshTokenCommand(refreshTokenPlaintext);
            var result = await sender.Send(command, ct);
            return HandleTokenResult(httpContext, result, cookieSettings, environment, tokenGenerator);
        }).RequireRateLimiting(RateLimitPolicies.Refresh);

        group.MapPost("logout", async (
            HttpContext httpContext,
            ISender sender,
            IOptions<RefreshCookieSettings> cookieSettingsOptions,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            var cookieSettings = cookieSettingsOptions.Value;

            if (!httpContext.Request.Cookies.TryGetValue(cookieSettings.CookieName, out var refreshTokenPlaintext)
                || string.IsNullOrWhiteSpace(refreshTokenPlaintext))
            {
                // Nothing to revoke and nothing ambient to protect via CSRF — idempotent success,
                // matching a caller who is already logged out. Still clear defensively in case a
                // stray CSRF cookie is left behind without its paired refresh cookie.
                AuthCookieWriter.ClearRefreshTokenCookie(httpContext, cookieSettings, environment);
                CsrfCookieWriter.ClearCsrfCookie(httpContext, cookieSettings, environment);
                return Results.Ok();
            }

            if (!CsrfCookieWriter.ValidateCsrfToken(httpContext))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var command = new LogoutCommand(refreshTokenPlaintext);
            var result = await sender.Send(command, ct);

            AuthCookieWriter.ClearRefreshTokenCookie(httpContext, cookieSettings, environment);
            CsrfCookieWriter.ClearCsrfCookie(httpContext, cookieSettings, environment);

            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireRateLimiting(RateLimitPolicies.Refresh);

        group.MapPost("verify-email", async (
            [FromBody] VerifyEmailRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new VerifyEmailCommand(request.Token);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireRateLimiting(RateLimitPolicies.AccountActions);

        group.MapPost("forgot-password", async (
            [FromBody] ForgotPasswordRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var command = new ForgotPasswordCommand(request.Email, ipAddress, request.TurnstileToken);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireRateLimiting(RateLimitPolicies.AccountActions);

        group.MapPost("reset-password", async (
            [FromBody] ResetPasswordRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ResetPasswordCommand(request.Token, request.NewPassword);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireRateLimiting(RateLimitPolicies.AccountActions);

        group.MapPost("resend-verification", async (
            [FromBody] ResendVerificationRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var command = new ResendVerificationCommand(request.Email, ipAddress, request.TurnstileToken);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireRateLimiting(RateLimitPolicies.AccountActions);

        group.MapGet("me", async (
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMeQuery(), ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireAuthorization();

        group.MapPatch("me", async (
            [FromBody] UpdateProfileRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdateProfileCommand(
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.PreferredLanguage,
                request.PreferredCurrency);

            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireAuthorization();

        group.MapPost("change-password", async (
            [FromBody] ChangePasswordRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ChangePasswordCommand(
                request.CurrentPassword,
                request.NewPassword);

            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireAuthorization();

        group.MapGet("sessions", async (
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSessionsQuery(), ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireAuthorization();

        group.MapPost("sessions/revoke-all", async (
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new RevokeAllSessionsCommand(), ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }).RequireAuthorization();

        return group;
    }

    private static GeoLocationResult? GetGeoLocation(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(GeoLocationHttpContextKeys.ResolvedGeoLocation, out var value)
            ? value as GeoLocationResult
            : null;

    /// <summary>
    /// Writes (or rotates) both the HttpOnly refresh cookie and its paired non-HttpOnly CSRF cookie —
    /// every token-issuing response (login, google, admin bypass, admin OTP verify, refresh) writes
    /// both together so the CSRF token is always in sync with whichever refresh token is currently live.
    /// </summary>
    private static void WriteAuthCookies(
        HttpContext httpContext,
        AuthTokenResponse tokens,
        RefreshCookieSettings cookieSettings,
        IHostEnvironment environment,
        ITokenGenerator tokenGenerator)
    {
        AuthCookieWriter.WriteRefreshTokenCookie(
            httpContext, cookieSettings, environment, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        CsrfCookieWriter.WriteCsrfCookie(httpContext, cookieSettings, environment, tokenGenerator);
    }

    /// <summary>
    /// Maps the internal <see cref="AuthTokenResponse"/> (which carries the plaintext refresh token)
    /// to the client-facing shape — deliberately omits <c>RefreshToken</c>, which travels exclusively
    /// via the HttpOnly cookie written by <see cref="WriteAuthCookies"/>.
    /// </summary>
    private static AccessTokenResponseDto ToAccessTokenResponse(AuthTokenResponse tokens) =>
        new(tokens.AccessToken, tokens.ExpiresAt);

    /// <summary>
    /// Shared success/failure mapping for every endpoint that issues a <see cref="Result{AuthTokenResponse}"/>:
    /// on success, writes the refresh/CSRF cookies and returns only the access token to the caller;
    /// on failure, defers to the existing localized error-payload convention. The refresh token in
    /// <c>result.Value</c> is never touched in the failure branch of <c>LocalizedEndpointResults.FromResult</c>,
    /// so this can never leak it even indirectly.
    /// </summary>
    private static IResult HandleTokenResult(
        HttpContext httpContext,
        Result<AuthTokenResponse> result,
        RefreshCookieSettings cookieSettings,
        IHostEnvironment environment,
        ITokenGenerator tokenGenerator)
    {
        if (!result.IsSuccess)
        {
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }

        WriteAuthCookies(httpContext, result.Value, cookieSettings, environment, tokenGenerator);
        return Results.Ok(ToAccessTokenResponse(result.Value));
    }
}

/// <summary>
/// Represents a registration request.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? ReferralCode = null,
    string TurnstileToken = "");

/// <summary>
/// Represents a login request.
/// </summary>
public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

/// <summary>
/// Represents a Google sign-in request.
/// </summary>
public sealed record GoogleLoginRequest(string IdToken);

/// <summary>
/// The client-facing shape of a successful login/refresh/OTP-verify response. Deliberately excludes
/// the refresh token — it travels exclusively via the HttpOnly <c>hambox_rt</c> cookie written by
/// <see cref="AuthCookieWriter"/>, never in a JSON body. <c>refresh</c>/<c>logout</c> themselves take
/// no request body at all: the refresh token is read from that same cookie, not from JSON.
/// </summary>
public sealed record AccessTokenResponseDto(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Represents an email verification request. The token travels in the request body (not the query
/// string) so it is never written into <c>ApiRequestLogs</c>' logged <c>Path + QueryString</c>,
/// browser history for the API call itself, or referrer headers.
/// </summary>
public sealed record VerifyEmailRequest(string Token);

/// <summary>
/// Represents a forgot password request.
/// </summary>
public sealed record ForgotPasswordRequest(string Email, string TurnstileToken = "");

/// <summary>
/// Represents a reset password request.
/// </summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>
/// Represents a resend verification email request.
/// </summary>
public sealed record ResendVerificationRequest(string Email, string TurnstileToken = "");

/// <summary>
/// Represents an update profile request.
/// </summary>
public sealed record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? PreferredLanguage,
    string? PreferredCurrency);

/// <summary>
/// Represents a change password request.
/// </summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Represents an admin OTP verification request.
/// </summary>
public sealed record VerifyAdminOtpRequest(Guid ChallengeId, string Code);

/// <summary>
/// Represents an admin OTP resend request.
/// </summary>
public sealed record ResendAdminOtpRequest(Guid ChallengeId);

/// <summary>
/// Represents a maintenance-mode bypass request.
/// </summary>
public sealed record MaintenanceBypassRequest(string Password);

/// <summary>
/// Client-facing shape of <see cref="AdminLoginChallengeResponse"/> — identical except
/// <c>Token</c> is the trimmed <see cref="AccessTokenResponseDto"/>, never the raw
/// <see cref="AuthTokenResponse"/> (which would leak the refresh token).
/// </summary>
public sealed record AdminLoginChallengeResponseDto(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResendAvailableAt,
    string MaskedEmail,
    AccessTokenResponseDto? Token = null);
