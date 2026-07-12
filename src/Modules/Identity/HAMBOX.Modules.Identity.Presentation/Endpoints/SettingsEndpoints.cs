using Asp.Versioning.Builder;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Features.PlatformSettings;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Text.Json;

namespace HAMBOX.Modules.Identity.Presentation.Endpoints;

internal static class SettingsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var adminGroup = app.MapGroup("api/v{version:apiVersion}/settings")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Settings")
            .HasApiVersion(1)
            .RequireAuthorization();

        adminGroup.MapGet("", async Task<IResult> (ISender sender) =>
        {
            var result = await sender.Send(new GetPlatformSettingsQuery());
            return MapResult(result);
        })
        .WithName("GetPlatformSettings")
        .RequirePermission(PermissionConstants.Settings.View);

        adminGroup.MapGet("audit", async Task<IResult> (
            ISender sender,
            [FromQuery] int take = 50) =>
        {
            var result = await sender.Send(new GetPlatformSettingsAuditQuery(take));
            return MapResult(result);
        })
        .WithName("GetPlatformSettingsAudit")
        .RequirePermission(PermissionConstants.Settings.View);

        adminGroup.MapGet("{categoryKey}", async Task<IResult> (
            string categoryKey,
            ISender sender) =>
        {
            var result = await sender.Send(new GetPlatformSettingsCategoryQuery(categoryKey));
            return MapResult(result);
        })
        .WithName("GetPlatformSettingsCategory")
        .RequirePermission(PermissionConstants.Settings.View);

        adminGroup.MapPut("{categoryKey}", async Task<IResult> (
            string categoryKey,
            HttpContext httpContext,
            [FromBody] JsonElement payload,
            ISender sender) =>
        {
            var (userId, displayName) = GetActor(httpContext);
            var result = await sender.Send(new UpdatePlatformSettingsCategoryCommand(
                categoryKey,
                payload.GetRawText(),
                userId,
                displayName));
            return MapResult(result);
        })
        .WithName("UpdatePlatformSettingsCategory")
        .RequirePermission(PermissionConstants.Settings.Edit);

        adminGroup.MapPost("{categoryKey}/restore-defaults", async Task<IResult> (
            string categoryKey,
            HttpContext httpContext,
            ISender sender) =>
        {
            var (userId, displayName) = GetActor(httpContext);
            var result = await sender.Send(new RestorePlatformSettingsCategoryCommand(
                categoryKey,
                userId,
                displayName));
            return MapResult(result);
        })
        .WithName("RestorePlatformSettingsCategory")
        .RequirePermission(PermissionConstants.Settings.Edit);

        adminGroup.MapPost("email/test", async Task<IResult> (
            [FromBody] TestEmailRequest body,
            ISender sender) =>
        {
            if (string.IsNullOrWhiteSpace(body.TestEmail))
            {
                return Results.BadRequest(new { error = "TestEmail is required." });
            }

            try
            {
                var result = await sender.Send(new TestPlatformSettingsEmailCommand(body.TestEmail));
                return MapResult(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("TestPlatformSettingsEmail")
        .RequirePermission(PermissionConstants.Settings.Edit);

        var publicGroup = app.MapGroup("api/v{version:apiVersion}/platform-settings")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Platform Settings")
            .HasApiVersion(1)
            .AllowAnonymous();

        publicGroup.MapGet("", async Task<IResult> (ISender sender) =>
        {
            var result = await sender.Send(new GetPublicPlatformSettingsQuery());
            return MapResult(result);
        })
        .WithName("GetPublicPlatformSettings");
    }

    private static (string? UserId, string? DisplayName) GetActor(HttpContext httpContext)
    {
        var user = httpContext.User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        var email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");
        return (userId, email);
    }

    private static IResult MapResult<T>(Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : MapFailure(result.Error);

    private static IResult MapResult(Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : MapFailure(result.Error);

    private static IResult MapFailure(Error error)
    {
        var problem = new ProblemDetails
        {
            Title = "Bad Request",
            Detail = error.Description,
            Type = error.Code,
            Status = StatusCodes.Status400BadRequest,
        };

        return TypedResults.Problem(problem);
    }

    private sealed record TestEmailRequest(string TestEmail);
}
