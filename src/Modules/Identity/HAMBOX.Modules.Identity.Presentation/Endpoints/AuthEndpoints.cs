using HAMBOX.Infrastructure.Localization;
using HAMBOX.Modules.Identity.Application.Features.ChangePassword;
using HAMBOX.Modules.Identity.Application.Features.ForgotPassword;
using HAMBOX.Modules.Identity.Application.Features.GetMe;
using HAMBOX.Modules.Identity.Application.Features.Login;
using HAMBOX.Modules.Identity.Application.Features.Logout;
using HAMBOX.Modules.Identity.Application.Features.RefreshToken;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Application.Features.ResendVerification;
using HAMBOX.Modules.Identity.Application.Features.ResetPassword;
using HAMBOX.Modules.Identity.Application.Features.UpdateProfile;
using HAMBOX.Modules.Identity.Application.Features.VerifyEmail;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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
            var command = new RegisterCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName);

            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

        group.MapPost("login", async (
            [FromBody] LoginRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";

            var command = new LoginCommand(
                request.Email,
                request.Password,
                ipAddress,
                userAgent);

            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

        group.MapPost("refresh", async (
            [FromBody] RefreshRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RefreshTokenCommand(request.RefreshToken);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

        group.MapPost("logout", async (
            [FromBody] LogoutRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new LogoutCommand(request.RefreshToken);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
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
            var command = new ForgotPasswordCommand(request.Email);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

        group.MapPost("reset-password", async (
            [FromBody] ResetPasswordRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ResetPasswordCommand(request.Token, request.NewPassword);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

        group.MapPost("resend-verification", async (
            [FromBody] ResendVerificationRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ResendVerificationCommand(request.Email);
            var result = await sender.Send(command, ct);
            return LocalizedEndpointResults.FromResult(httpContext, result);
        });

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

        return group;
    }
}

/// <summary>
/// Represents a registration request.
/// </summary>
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);

/// <summary>
/// Represents a login request.
/// </summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Represents a refresh token request.
/// </summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Represents a logout request.
/// </summary>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// Represents a forgot password request.
/// </summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>
/// Represents a reset password request.
/// </summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>
/// Represents a resend verification email request.
/// </summary>
public sealed record ResendVerificationRequest(string Email);

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
