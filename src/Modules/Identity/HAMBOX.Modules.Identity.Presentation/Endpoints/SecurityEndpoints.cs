using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;
using HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;
using HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;
using HAMBOX.Modules.Identity.Application.Features.Security.CountryRestrictions;
using HAMBOX.Modules.Identity.Application.Features.Security.Devices;
using HAMBOX.Modules.Identity.Application.Features.Security.LoginHistory;
using HAMBOX.Modules.Identity.Application.Features.Security.OtpEvents;
using HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;
using HAMBOX.Modules.Identity.Application.Features.Security.Sessions;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Identity.Presentation.Endpoints;

/// <summary>
/// Registers Security Center endpoints: user status management, email/IP/country blocklists,
/// the security event log, and the security dashboard.
/// </summary>
internal static class SecurityEndpoints
{
    public static void MapSecurityEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/security")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Security")
            .HasApiVersion(1)
            .RequireAuthorization();

        // ──── Dashboard & Events ────────────────────────────────────────
        group.MapGet("dashboard", async Task<IResult> (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSecurityDashboardQuery(), ct);
            return MapResult(result);
        })
        .WithName("GetSecurityDashboard")
        .RequirePermission(PermissionConstants.Security.View);

        group.MapGet("events", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? eventType,
            [FromQuery] string? severity,
            [FromQuery] DateTimeOffset? fromUtc,
            [FromQuery] DateTimeOffset? toUtc,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            [FromQuery] string? minSeverity,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;

            var result = await sender.Send(
                new GetSecurityEventsQuery(pageNumber, pageSize, eventType, severity, fromUtc, toUtc, searchTerm, status, minSeverity), ct);
            return MapResult(result);
        })
        .WithName("GetSecurityEvents")
        .RequirePermission(PermissionConstants.Security.ViewEvents);

        group.MapGet("otp-events", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] Guid? userId,
            [FromQuery] string? purpose,
            [FromQuery] string? status,
            [FromQuery] DateTimeOffset? fromUtc,
            [FromQuery] DateTimeOffset? toUtc,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;

            var result = await sender.Send(
                new GetCustomerOtpEventsQuery(pageNumber, pageSize, userId, purpose, status, fromUtc, toUtc), ct);
            return MapResult(result);
        })
        .WithName("GetCustomerOtpEvents")
        .RequirePermission(PermissionConstants.Security.ViewEvents);

        group.MapPut("events/{id:guid}/status", async Task<IResult> (
            Guid id,
            [FromBody] UpdateSecurityEventStatusRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<SecurityEventStatus>(request.Status, ignoreCase: true, out var status))
            {
                status = SecurityEventStatus.Open;
            }

            var result = await sender.Send(new UpdateSecurityEventStatusCommand(id, status, request.Notes), ct);
            return MapResult(result);
        })
        .WithName("UpdateSecurityEventStatus")
        .RequirePermission(PermissionConstants.Security.ManageAlerts);

        // ──── Login History ─────────────────────────────────────────────
        group.MapGet("login-history", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] Guid? userId,
            [FromQuery] bool? isSuccessful,
            [FromQuery] string? countryCode,
            [FromQuery] string? riskLevel,
            [FromQuery] string? fingerprint,
            [FromQuery] DateTimeOffset? fromUtc,
            [FromQuery] DateTimeOffset? toUtc,
            [FromQuery] string? searchTerm,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;

            var result = await sender.Send(
                new GetLoginHistoryQuery(pageNumber, pageSize, userId, isSuccessful, countryCode, riskLevel, fingerprint, fromUtc, toUtc, searchTerm),
                ct);
            return MapResult(result);
        })
        .WithName("GetLoginHistory")
        .RequirePermission(PermissionConstants.Security.ViewEvents);

        // ──── Trusted Devices ────────────────────────────────────────────
        group.MapGet("devices", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] Guid? userId,
            [FromQuery] bool? isTrusted,
            [FromQuery] bool? isBlocked,
            [FromQuery] string? searchTerm,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : pageSize;

            var result = await sender.Send(
                new GetTrustedDevicesQuery(pageNumber, pageSize, userId, isTrusted, isBlocked, searchTerm), ct);
            return MapResult(result);
        })
        .WithName("GetTrustedDevices")
        .RequirePermission(PermissionConstants.Security.ManageDevices);

        group.MapPost("devices/{id:guid}/trust", async Task<IResult> (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new TrustDeviceCommand(id), ct);
            return MapResult(result);
        })
        .WithName("TrustDevice")
        .RequirePermission(PermissionConstants.Security.ManageDevices);

        group.MapPost("devices/{id:guid}/untrust", async Task<IResult> (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UntrustDeviceCommand(id), ct);
            return MapResult(result);
        })
        .WithName("UntrustDevice")
        .RequirePermission(PermissionConstants.Security.ManageDevices);

        group.MapPost("devices/{id:guid}/block", async Task<IResult> (
            Guid id,
            [FromBody] BlockDeviceRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new BlockDeviceCommand(id, request.Reason, GetIpAddress(httpContext)), ct);
            return MapResult(result);
        })
        .WithName("BlockDevice")
        .RequirePermission(PermissionConstants.Security.ManageDevices);

        group.MapPost("devices/{id:guid}/unblock", async Task<IResult> (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UnblockDeviceCommand(id), ct);
            return MapResult(result);
        })
        .WithName("UnblockDevice")
        .RequirePermission(PermissionConstants.Security.ManageDevices);

        // ──── Blocked Users ──────────────────────────────────────────────
        group.MapGet("users", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromQuery] string? status,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var result = await sender.Send(new GetBlockedUsersQuery(pageNumber, pageSize, searchTerm, status), ct);
            return MapResult(result);
        })
        .WithName("GetBlockedUsers")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapPost("users/{userId:guid}/block", async Task<IResult> (
            Guid userId,
            [FromBody] BlockUserRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new BlockUserCommand(userId, request.Reason, request.Notes, request.ExpiresOnUtc, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("BlockUser")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapPost("users/{userId:guid}/suspend", async Task<IResult> (
            Guid userId,
            [FromBody] SuspendUserRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new SuspendUserCommand(userId, request.Reason, request.Notes, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("SuspendUser")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapPost("users/{userId:guid}/ban", async Task<IResult> (
            Guid userId,
            [FromBody] BanUserRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new BanUserCommand(userId, request.Reason, request.Notes, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("BanUser")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapPost("users/{userId:guid}/unblock", async Task<IResult> (
            Guid userId,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new UnblockUserCommand(userId, GetIpAddress(httpContext)), ct);
            return MapResult(result);
        })
        .WithName("UnblockUser")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        // ──── User Sessions (admin) ──────────────────────────────────────
        group.MapGet("users/{userId:guid}/sessions", async Task<IResult> (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUserSessionsQuery(userId), ct);
            return MapResult(result);
        })
        .WithName("GetUserSessions")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapPost("users/{userId:guid}/sessions/revoke-all", async Task<IResult> (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RevokeAllUserSessionsCommand(userId), ct);
            return MapResult(result);
        })
        .WithName("RevokeAllUserSessions")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        group.MapDelete("users/{userId:guid}/sessions/{sessionId:guid}", async Task<IResult> (
            Guid userId, Guid sessionId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RevokeUserSessionCommand(userId, sessionId), ct);
            return MapResult(result);
        })
        .WithName("RevokeUserSession")
        .RequirePermission(PermissionConstants.Security.ManageUsers);

        // ──── Blocked Emails ─────────────────────────────────────────────
        group.MapGet("emails", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var result = await sender.Send(new GetBlockedEmailsQuery(pageNumber, pageSize, searchTerm), ct);
            return MapResult(result);
        })
        .WithName("GetBlockedEmails")
        .RequirePermission(PermissionConstants.Security.ManageEmails);

        group.MapPost("emails", async Task<IResult> (
            [FromBody] CreateBlockedEmailRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateBlockedEmailCommand(
                request.Pattern, request.Reason, request.Notes, request.ExpiresOnUtc, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapCreatedResult(result, "emails");
        })
        .WithName("CreateBlockedEmail")
        .RequirePermission(PermissionConstants.Security.ManageEmails);

        group.MapDelete("emails/{id:guid}", async Task<IResult> (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteBlockedEmailCommand(id), ct);
            return MapResult(result);
        })
        .WithName("DeleteBlockedEmail")
        .RequirePermission(PermissionConstants.Security.ManageEmails);

        // ──── Blocked IPs ────────────────────────────────────────────────
        group.MapGet("ips", async Task<IResult> (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            ISender sender,
            CancellationToken ct) =>
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var result = await sender.Send(new GetBlockedIpsQuery(pageNumber, pageSize, searchTerm), ct);
            return MapResult(result);
        })
        .WithName("GetBlockedIps")
        .RequirePermission(PermissionConstants.Security.ManageIPs);

        group.MapPost("ips", async Task<IResult> (
            [FromBody] CreateBlockedIpRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateBlockedIpCommand(
                request.CidrOrAddress, request.Reason, request.Notes, request.ExpiresOnUtc, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapCreatedResult(result, "ips");
        })
        .WithName("CreateBlockedIp")
        .RequirePermission(PermissionConstants.Security.ManageIPs);

        group.MapDelete("ips/{id:guid}", async Task<IResult> (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteBlockedIpCommand(id), ct);
            return MapResult(result);
        })
        .WithName("DeleteBlockedIp")
        .RequirePermission(PermissionConstants.Security.ManageIPs);

        // ──── Country Restrictions ───────────────────────────────────────
        group.MapGet("countries", async Task<IResult> (
            [FromQuery] string? searchTerm,
            [FromQuery] bool overriddenOnly,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCountryRestrictionsQuery(searchTerm, overriddenOnly), ct);
            return MapResult(result);
        })
        .WithName("GetCountryRestrictions")
        .RequirePermission(PermissionConstants.Security.ManageCountries);

        group.MapPut("countries/{countryCode}", async Task<IResult> (
            string countryCode,
            [FromBody] SetCountryRestrictionRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            // An unparsable status is passed through as an out-of-range enum value so the
            // command's existing IsInEnum() validator rejects it through the normal pipeline.
            Enum.TryParse<CountryRestrictionStatus>(request.Status, ignoreCase: true, out var status);

            var command = new SetCountryRestrictionCommand(
                countryCode, status, request.Reason, request.Notes, request.ExpiresOnUtc, GetIpAddress(httpContext));
            var result = await sender.Send(command, ct);
            return MapResult(result);
        })
        .WithName("SetCountryRestriction")
        .RequirePermission(PermissionConstants.Security.ManageCountries);
    }

    private static string? GetIpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString();

    private static IResult MapResult(Result result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.NoContent();
        }

        return MapFailure(result.Error);
    }

    private static IResult MapResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return MapFailure(result.Error);
    }

    private static IResult MapCreatedResult(Result<Guid> result, string resourceSegment)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Created($"/api/v1/security/{resourceSegment}/{result.Value}", result.Value);
        }

        return MapFailure(result.Error);
    }

    private static IResult MapFailure(Error error)
    {
        var problem = new ProblemDetails
        {
            Title = GetTitle(error),
            Detail = error.Description,
            Type = error.Code,
            Status = GetStatusCode(error),
        };

        return TypedResults.Problem(problem);
    }

    private static int GetStatusCode(Error error) => error.Code switch
    {
        _ when error == IdentityErrors.UserNotFound ||
               error == IdentityErrors.CountryRestrictionNotFound ||
               error == IdentityErrors.BlockedEmailNotFound ||
               error == IdentityErrors.BlockedIpNotFound ||
               error == IdentityErrors.SessionNotFound ||
               error == IdentityErrors.TrustedDeviceNotFound ||
               error == IdentityErrors.SecurityEventNotFound => StatusCodes.Status404NotFound,
        _ when error == IdentityErrors.CannotRestrictOwner ||
               error == IdentityErrors.PermissionDenied => StatusCodes.Status403Forbidden,
        _ when error == IdentityErrors.AuthenticationRequired => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string GetTitle(Error error) => GetStatusCode(error) switch
    {
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        _ => "Bad Request",
    };
}
