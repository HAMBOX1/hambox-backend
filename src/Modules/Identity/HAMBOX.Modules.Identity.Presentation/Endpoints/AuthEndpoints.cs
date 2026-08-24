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
            ITokenGenerator tokenGenerator,
            IOptions<RefreshCookieSettings> cookieSettings,
            IHostEnvironment environment,
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
            return ToAuthCookieResult(httpContext, result, tokenGenerator, cookieSettings.Value, environment);
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("google", async (
            [FromBody] GoogleLoginRequest request,
            HttpContext httpContext,
            ISender sender,
            ITokenGenerator tokenGenerator,
            IOptions<RefreshCookieSettings> cookieSettings,
            IHostEnvironment environment,
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
            return ToAuthCookieResult(httpContext, result, tokenGenerator, cookieSettings.Value, environment);
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("admin/login", async (
            [FromBody] LoginRequest request,
            HttpContext httpContext,
            ISender sender,
            ITokenGenerator tokenGenerator,
            IOptions<RefreshCookieSettings> cookieSettings,
            IHostEnvironment environment,
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

            // Admin OTP disabled via Platform Settings bypasses the challenge and issues tokens
            // immediately — same cookie treatment as every other token-issuing branch below.
            AccessTokenResponse? accessResponse = null;
            if (result.Value.Token is { } tokens)
            {
                AuthCookieWriter.WriteRefreshTokenCookie(
                    httpContext, cookieSettings.Value, environment, tokens.RefreshToken, tokens.RefreshExpiresAt);
                CsrfCookieWriter.WriteCsrfCookie(httpContext, cookieSettings.Value, environment, tokenGenerator);
                accessResponse = new AccessTokenResponse(tokens.AccessToken, tokens.ExpiresAt);
            }

            return Results.Ok(new AdminLoginChallengeApiResponse(
                result.Value.ChallengeId,
                result.Value.ExpiresAt,
                result.Value.ResendAvailableAt,
                result.Value.MaskedEmail,
                accessResponse));
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("admin/verify-otp", async (
            [FromBody] VerifyAdminOtpRequest request,
            HttpContext httpContext,
            ISender sender,
            ITokenGenerator tokenGenerator,
            IOptions<RefreshCookieSettings> cookieSettings,
            IHostEnvironment environment,
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
            return ToAuthCookieResult(httpContext, result, tokenGenerator, cookieSettings.Value, environment);
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
            ITokenGenerator tokenGenerator,
            IOptions<RefreshCookieSettings> cookieSettings,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            if (!httpContext.Request.Cookies.TryGetValue(cookieSettings.Value.CookieName, out var refreshToken)
                || string.IsNullOrWhiteSpace(refreshToken))
            {
                return Results.Unauthorized();
            }

            if (!CsrfCookieWriter.ValidateCsrfToken(httpContext))
            {
                return Results.Forbid();
            }

            var command = new RefreshTokenCommand(refreshToken);
            var result = await sender.Send(command, ct);

            if (!result.IsSuccess)
            {
                // The presented refresh token was invalid/expired/reused — never leave a stale
                // cookie behind for a token that no longer works.
                AuthCookieWriter.ClearRefreshTokenCookie(httpContext, cookieSettings.Value, environment);
            }

            return ToAuthCookieResult(httpContext, result, tokenGenerator, cookieSettings.Value, environment);
        }).RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("logout", async (
            HttpContext httpContext,
            ISender sender,
            IOptions<RefreshCookieSettings> cookieSettings,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            var hasCookie = httpContext.Request.Cookies.TryGetValue(
                cookieSettings.Value.CookieName, out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken);

            if (hasCookie)
            {
                if (!CsrfCookieWriter.ValidateCsrfToken(httpContext))
                {
                    return Results.Forbid();
                }

                var command = new LogoutCommand(refreshToken!);
                await sender.Send(command, ct);
            }

            AuthCookieWriter.ClearRefreshTokenCookie(httpContext, cookieSettings.Value, environment);
            CsrfCookieWriter.ClearCsrfCookie(httpContext, cookieSettings.Value, environment);
            return Results.Ok();
        });

        group.MapPost("verify-email", async (
            [FromQuery] string token,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new VerifyEmailCommand(token);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

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
    /// Shared success/failure mapping for every endpoint that issues tokens (login, google,
    /// admin/verify-otp, refresh): on failure, behaves exactly like the existing
    /// <see cref="LocalizedEndpointResults.FromResult{T}"/> convention used everywhere else in this
    /// file. On success, the refresh token is written as an HttpOnly cookie (never serialized), a
    /// matching CSRF/XSRF cookie is issued alongside it, and only the slim
    /// <see cref="AccessTokenResponse"/> goes into the JSON body.
    /// </summary>
    private static IResult ToAuthCookieResult(
        HttpContext httpContext,
        Result<AuthTokenResponse> result,
        ITokenGenerator tokenGenerator,
        RefreshCookieSettings cookieSettings,
        IHostEnvironment environment)
    {
        if (!result.IsSuccess)
        {
            return LocalizedEndpointResults.FromResult(httpContext, result);
        }

        AuthCookieWriter.WriteRefreshTokenCookie(
            httpContext, cookieSettings, environment, result.Value.RefreshToken, result.Value.RefreshExpiresAt);
        CsrfCookieWriter.WriteCsrfCookie(httpContext, cookieSettings, environment, tokenGenerator);

        return Results.Ok(new AccessTokenResponse(result.Value.AccessToken, result.Value.ExpiresAt));
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
    string TurnstileToken,
    string? ReferralCode = null);

/// <summary>
/// Represents a login request.
/// </summary>
public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

/// <summary>
/// Represents a Google sign-in request.
/// </summary>
public sealed record GoogleLoginRequest(string IdToken);

/// <summary>
/// The public response shape for every endpoint that issues tokens — deliberately does not include
/// the refresh token, which is transported exclusively as an HttpOnly cookie (see
/// <see cref="AuthCookieWriter"/>) and must never appear in a JSON response body.
/// </summary>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// The public response shape for <c>admin/login</c> — mirrors the internal
/// <see cref="AdminLoginChallengeResponse"/> but with <see cref="AccessTokenResponse"/> in place of
/// the internal <see cref="AuthTokenResponse"/>, for the same reason as <see cref="AccessTokenResponse"/>
/// itself.
/// </summary>
public sealed record AdminLoginChallengeApiResponse(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResendAvailableAt,
    string MaskedEmail,
    AccessTokenResponse? Token);

/// <summary>
/// Represents a forgot password request.
/// </summary>
public sealed record ForgotPasswordRequest(string Email, string TurnstileToken);

/// <summary>
/// Represents a reset password request.
/// </summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>
/// Represents a resend verification email request.
/// </summary>
public sealed record ResendVerificationRequest(string Email, string TurnstileToken);

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
