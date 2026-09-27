using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// WhatsApp channel provider backed by the Zoho CPaaS WhatsApp REST API
/// (<c>POST /v1.1/whatsapp</c>, body: <c>from</c>, <c>to</c>, <c>template_key</c>, <c>merge_info</c>).
/// WhatsApp only delivers pre-approved templates, so a WhatsApp-channel <c>CommunicationTemplate</c> is
/// authored like this: its <b>Subject</b> is the Zoho <c>template_key</c>, and its <b>Body</b> is a JSON object
/// of merge values (rendered through the normal placeholder engine), e.g.
/// <c>{"order_number":"{{OrderNumber}}","total":"{{Total}}"}</c>.
/// Inert until <c>ZohoCpaas:WhatsApp:ApiKey</c> and <c>ZohoCpaas:WhatsApp:From</c> are configured — with no
/// configuration every send just returns a failure result and nothing leaves the server.
/// </summary>
internal sealed class ZohoWhatsAppCommunicationProvider(
    IIdentityDbContext identityDb,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ZohoWhatsAppCommunicationProvider> logger) : ICommunicationProvider
{
    private const string DefaultEndpoint = "https://cpaas.zoho.com/v1.1/whatsapp";
    private const string AuthScheme = "Zoho-enczapikey";

    public string ChannelKey => CommunicationChannels.WhatsApp;

    public async Task<CommunicationDeliveryResult> SendAsync(CommunicationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        var apiKey = (configuration["ZohoCpaas:WhatsApp:ApiKey"] ?? string.Empty)
            .Replace(AuthScheme, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        var from = configuration["ZohoCpaas:WhatsApp:From"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from))
        {
            return CommunicationDeliveryResult.Fail("WhatsApp delivery is not configured.");
        }

        if (!Guid.TryParse(context.UserId, out var userId))
        {
            return CommunicationDeliveryResult.Fail("Invalid user id.");
        }

        var rawPhone = await identityDb.Users
            .Where(u => u.Id == userId)
            .Select(u => u.PhoneNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var phone = NormalizePhone(rawPhone);
        if (phone is null)
        {
            return CommunicationDeliveryResult.Fail("User has no valid phone number on file.");
        }

        var templateKey = context.Subject?.Trim();
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return CommunicationDeliveryResult.Fail("WhatsApp template has no template key (Subject).");
        }

        JsonNode? mergeInfo;
        try
        {
            mergeInfo = string.IsNullOrWhiteSpace(context.Body) ? new JsonObject() : JsonNode.Parse(context.Body);
        }
        catch (JsonException)
        {
            return CommunicationDeliveryResult.Fail("WhatsApp template body must be a JSON object of merge values.");
        }

        if (mergeInfo is not JsonObject)
        {
            return CommunicationDeliveryResult.Fail("WhatsApp template body must be a JSON object of merge values.");
        }

        var payload = new JsonObject
        {
            ["from"] = from,
            ["to"] = phone,
            ["template_key"] = templateKey,
            ["merge_info"] = mergeInfo
        };

        var endpoint = configuration["ZohoCpaas:WhatsApp:Endpoint"] is { Length: > 0 } configured ? configured : DefaultEndpoint;
        var maskedPhone = MaskPhone(phone);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"{AuthScheme} {apiKey}");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Sent WhatsApp template {TemplateKey} to {MaskedPhone} via Zoho CPaaS. MessageId={MessageId} CorrelationId={CorrelationId}",
                    templateKey,
                    maskedPhone,
                    context.MessageId,
                    context.CorrelationId);

                return CommunicationDeliveryResult.Ok();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var snippet = body.Length > 300 ? body[..300] : body;
            logger.LogError(
                "Zoho CPaaS rejected WhatsApp template {TemplateKey} to {MaskedPhone}: HTTP {StatusCode} {ResponseBody}. MessageId={MessageId}",
                templateKey,
                maskedPhone,
                (int)response.StatusCode,
                snippet,
                context.MessageId);

            return CommunicationDeliveryResult.Fail($"Zoho CPaaS returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp template {TemplateKey} to {MaskedPhone} via Zoho CPaaS. MessageId={MessageId}", templateKey, maskedPhone, context.MessageId);
            return CommunicationDeliveryResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Best-effort conversion to a plain international number (digits, no '+'). Numbers already written with
    /// a country code (<c>+20…</c> / <c>0020…</c>) are kept; a local Egyptian mobile (<c>01XXXXXXXXX</c>) gets
    /// the <c>20</c> prefix. Anything else that is not 8–15 digits is rejected rather than guessed at.
    /// </summary>
    internal static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.Length == 11 && digits.StartsWith("01", StringComparison.Ordinal))
        {
            digits = "20" + digits[1..];
        }

        return digits.Length is >= 8 and <= 15 ? digits : null;
    }

    private static string MaskPhone(string phone) =>
        phone.Length <= 4 ? "****" : new string('*', phone.Length - 4) + phone[^4..];
}
