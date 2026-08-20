using Asp.Versioning.Builder;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Messaging.Application.Contracts;
using HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.GetWhatsAppBotConfiguration;
using HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.UpdateWhatsAppBotConfiguration;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Messaging.Presentation.Endpoints;

/// <summary>
/// Admin-only configuration for the WhatsApp bot's presentation (welcome/fallback text, menu item
/// labels/order/enabled state) — never the inbound webhook, never bot behavior. Gated by
/// <c>WhatsAppBot.View</c>/<c>WhatsAppBot.Edit</c>; a normal customer-context token cannot reach these
/// (same <c>.RequirePermission</c> policy layer every other admin endpoint uses).
/// </summary>
internal static class WhatsAppBotConfigurationEndpoints
{
    public static void MapWhatsAppBotConfigurationEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/whatsapp/config")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("WhatsApp Bot Configuration")
            .HasApiVersion(1);

        group.MapGet("", async Task<IResult> (ISender sender) =>
            MapResult(await sender.Send(new GetWhatsAppBotConfigurationQuery())))
            .RequirePermission(PermissionConstants.WhatsAppBot.View);

        group.MapPut("", async Task<IResult> (
            [FromBody] UpdateWhatsAppBotConfigurationRequest request, ISender sender) =>
            MapResult(await sender.Send(new UpdateWhatsAppBotConfigurationCommand(
                request.WelcomeMessageEn,
                request.WelcomeMessageAr,
                request.FallbackMessageEn,
                request.FallbackMessageAr,
                request.Items.Select(i => new WhatsAppMenuItemUpdate(i.Action, i.IsEnabled, i.LabelEn, i.LabelAr)).ToList()))))
            .RequirePermission(PermissionConstants.WhatsAppBot.Edit);
    }

    private static IResult MapResult<T>(Result<T> result) =>
        result.IsSuccess ? Results.Json(result.Value) : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
}

internal sealed record WhatsAppMenuItemRequest(WhatsAppMenuAction Action, bool IsEnabled, string LabelEn, string LabelAr);

internal sealed record UpdateWhatsAppBotConfigurationRequest(
    string WelcomeMessageEn,
    string WelcomeMessageAr,
    string FallbackMessageEn,
    string FallbackMessageAr,
    IReadOnlyList<WhatsAppMenuItemRequest> Items);
