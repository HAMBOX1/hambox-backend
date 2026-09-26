using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Sends transactional emails through the Zoho CPaaS REST API (<c>POST /v1.1/email</c>).
/// Selected by <see cref="PlatformRoutingEmailService"/> when <c>ZohoCpaas:ApiKey</c> is configured.
/// Message content is produced by the same <see cref="EmailMessageBuilder"/> used for SMTP, so the
/// subject/body of every email is identical regardless of provider.
/// </summary>
internal sealed class ZohoCpaasEmailService(
    IPlatformSettingsService platformSettings,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ZohoCpaasEmailService> logger) : IEmailService
{
    private const string DefaultEndpoint = "https://cpaas.zoho.com/v1.1/email";
    private const string AuthScheme = "Zoho-enczapikey";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public async Task<bool> SendEmailVerificationAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildVerificationMessage(settings, email, token, expiresAt);
        return await SendCoreAsync("EmailVerification", userId, email, message, settings, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendPasswordResetAsync(
        Guid userId,
        string email,
        DateTimeOffset expiresAt,
        string token,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildPasswordResetMessage(settings, email, token, expiresAt);
        return await SendCoreAsync("PasswordReset", userId, email, message, settings, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SendAdminLoginOtpAsync(
        Guid userId,
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildAdminOtpMessage(settings, email, code, expiresAt);
        return await SendCoreAsync("AdminLoginOtp", userId, email, message, settings, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendTemplatedEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await platformSettings.GetEmailSettingsForLegacyAsync(cancellationToken);
        var message = EmailMessageBuilder.BuildTemplatedMessage(settings, toEmail, subject, htmlBody);
        var sent = await SendCoreAsync("Templated", null, toEmail, message, settings, correlationId, cancellationToken);

        if (!sent)
        {
            throw new InvalidOperationException($"Failed to send templated email to {EmailLogHelper.MaskEmail(toEmail)}.");
        }
    }

    /// <summary>Returns <see langword="true"/> on a successful send; never throws.</summary>
    private async Task<bool> SendCoreAsync(
        string emailType,
        Guid? userId,
        string email,
        MimeMessage message,
        EmailSettings settings,
        string? correlationIdOverride,
        CancellationToken cancellationToken)
    {
        var correlationId = correlationIdOverride ?? GetCorrelationId();
        var maskedEmail = EmailLogHelper.MaskEmail(email);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var apiKey = (configuration["ZohoCpaas:ApiKey"] ?? string.Empty)
                .Replace(AuthScheme, string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
            var endpoint = configuration["ZohoCpaas:Endpoint"] is { Length: > 0 } configuredEndpoint
                ? configuredEndpoint
                : DefaultEndpoint;
            var fromAddress = configuration["ZohoCpaas:FromAddress"] is { Length: > 0 } configuredFrom
                ? configuredFrom
                : settings.FromAddress;
            var fromName = configuration["ZohoCpaas:FromName"] is { Length: > 0 } configuredName
                ? configuredName
                : settings.FromName;

            // Customers' replies to a notification land here instead of the (mailbox-less) sender address.
            var replyTo = configuration["ZohoCpaas:ReplyTo"] is { Length: > 0 } configuredReplyTo
                ? new[] { new ZohoAddress(configuredReplyTo, fromName) }
                : null;

            var payload = new ZohoEmailRequest(
                new ZohoAddress(fromAddress, fromName),
                [new ZohoRecipient(new ZohoAddress(email, email))],
                message.Subject,
                message.HtmlBody,
                message.TextBody,
                replyTo);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"{AuthScheme} {apiKey}");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var response = await client.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Sent {EmailType} email to {MaskedEmail} for user {UserId} via Zoho CPaaS in {ElapsedMs}ms. CorrelationId={CorrelationId}",
                    emailType,
                    maskedEmail,
                    userId,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);

                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Zoho CPaaS rejected {EmailType} email to {MaskedEmail} for user {UserId}: HTTP {StatusCode} {ResponseBody}. CorrelationId={CorrelationId}",
                emailType,
                maskedEmail,
                userId,
                (int)response.StatusCode,
                body.Length > 500 ? body[..500] : body,
                correlationId);

            return false;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Failed to send {EmailType} email to {MaskedEmail} for user {UserId} via Zoho CPaaS after {ElapsedMs}ms. CorrelationId={CorrelationId}",
                emailType,
                maskedEmail,
                userId,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            return false;
        }
    }

    private string? GetCorrelationId()
    {
        return httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var value) == true
            ? value?.ToString()
            : null;
    }

    private sealed record ZohoAddress(
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("name")] string Name);

    private sealed record ZohoRecipient(
        [property: JsonPropertyName("email_address")] ZohoAddress EmailAddress);

    private sealed record ZohoEmailRequest(
        [property: JsonPropertyName("from")] ZohoAddress From,
        [property: JsonPropertyName("to")] IReadOnlyList<ZohoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlbody")] string? HtmlBody,
        [property: JsonPropertyName("textbody")] string? TextBody,
        [property: JsonPropertyName("reply_to")] IReadOnlyList<ZohoAddress>? ReplyTo);
}
