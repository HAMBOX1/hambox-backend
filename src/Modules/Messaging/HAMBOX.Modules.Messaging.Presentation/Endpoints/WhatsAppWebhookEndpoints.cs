using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.RateLimiting;
using HAMBOX.Modules.Messaging.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Messaging.Presentation.Endpoints;

/// <summary>
/// The one inbound surface for the WhatsApp bot. Unversioned and un-prefixed with <c>api/v{version}</c>
/// (same exception the auth endpoints already use) because a webhook URL is a contract with an external
/// party (Meta), not a versioned business API. <c>GET</c> is Meta's documented subscription handshake;
/// <c>POST</c> is where inbound messages arrive.
/// <para>
/// Deliberately accepts a simple internal shape (<see cref="WhatsAppWebhookInboundRequest"/>) rather than
/// Meta's real nested Cloud API envelope, and skips payload signature verification — both are Meta-account
/// specific and cannot be implemented against real payloads until the owner supplies credentials. Adapting
/// this one endpoint's parsing is the extent of the work required once they do; nothing else in the
/// module (the engine, the session model, the menu logic) needs to change.
/// </para>
/// <para>
/// Known residual risk until signature verification lands: this endpoint cannot yet confirm a POST
/// actually came from Meta, so a caller can claim to be any phone number's <c>From</c>. The bot engine
/// does not treat that claim as identity on its own — Orders/Alerts/Support still require the phone
/// number's session to have completed its own email-code link, and an expired session drops that link
/// (see <c>WhatsAppConversationSession.Unlink</c>), bounding how long a spoofed phone number could ride
/// someone else's earlier verification. That bound is a mitigation, not a fix — real caller
/// authentication is still required before this is production-ready.
/// </para>
/// </summary>
public static class WhatsAppWebhookEndpoints
{
    public static void MapWhatsAppWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/webhooks/whatsapp").WithTags("WhatsApp Webhook").AllowAnonymous()
            .RequireRateLimiting(MessagingRateLimitPolicies.WhatsAppWebhook);

        group.MapGet("", (
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.mode")] string? mode,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.challenge")] string? challenge,
            IConfiguration configuration) =>
        {
            var configuredToken = configuration["WhatsAppSettings:VerifyToken"];

            if (mode == "subscribe" && !string.IsNullOrEmpty(configuredToken)
                && string.Equals(verifyToken, configuredToken, StringComparison.Ordinal))
            {
                return Results.Text(challenge ?? string.Empty);
            }

            return Results.Forbid();
        });

        group.MapPost("", async Task<IResult> (
            WhatsAppWebhookInboundRequest request,
            IWhatsAppBotEngine botEngine,
            ILogger<WhatsAppWebhookInboundRequest> logger,
            CancellationToken cancellationToken) =>
        {
            // 32 matches WhatsAppConversationSession.PhoneNumber's column length — reject here with a
            // clean 400 rather than letting an oversized value reach SaveChangesAsync and fail as an
            // unhandled DbUpdateException that the catch below would swallow silently.
            if (string.IsNullOrWhiteSpace(request.From) || request.From.Length > 32)
            {
                return Results.BadRequest();
            }

            // 4096 matches WhatsApp's own real-world text message limit, so this never rejects a
            // message a real Meta payload could ever contain. Without this cap an oversized Text
            // (unbounded — Kestrel's request-body limit is the only thing standing in the way) can
            // still reach WhatsAppBotEngine and blow past a downstream fixed-length column
            // (ContextJson is nvarchar(2000)) or a command's own FluentValidation MaximumLength (e.g.
            // support ticket Body), throwing from inside SaveChangesAsync with no reply ever sent —
            // the same silent-dead-end failure mode the From check above already guards against.
            if (request.Text is { Length: > 4096 })
            {
                return Results.BadRequest();
            }

            try
            {
                await botEngine.HandleInboundMessageAsync(
                    new WhatsAppInboundMessage(request.From.Trim(), request.Text ?? string.Empty), cancellationToken);
            }
            catch (Exception ex)
            {
                // A webhook must not make Meta retry-storm us over a bot-logic bug — log and ack.
                // Phone numbers are PII — never logged in full (see WhatsAppPhoneMasker).
                logger.LogError(ex, "Failed to process inbound WhatsApp message from {From}.", WhatsAppPhoneMasker.Mask(request.From));
            }

            return Results.Ok();
        });
    }
}

/// <summary>E.164 phone number and raw message text. Meta's real Cloud API envelope wraps this in
/// several layers (entry[].changes[].value.messages[].from/text.body) — mapping that shape onto this
/// record is the real-provider integration work, not a change to the bot engine.</summary>
public sealed record WhatsAppWebhookInboundRequest(string From, string? Text);
