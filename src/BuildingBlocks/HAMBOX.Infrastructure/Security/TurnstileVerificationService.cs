using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HAMBOX.Application.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Security;

/// <summary>
/// Calls Cloudflare's Turnstile Siteverify API. Registered as a typed <see cref="HttpClient"/>
/// (<c>AddHttpClient&lt;ITurnstileVerificationService, TurnstileVerificationService&gt;</c> in
/// <c>InfrastructureExtensions</c>) with its base address pointed at Cloudflare and a bounded timeout.
/// </summary>
internal sealed class TurnstileVerificationService(
    HttpClient httpClient,
    IOptions<TurnstileSettings> options,
    ILogger<TurnstileVerificationService> logger) : ITurnstileVerificationService
{
    private const string SiteverifyPath = "turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, string? expectedAction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Turnstile verification rejected: no token was supplied.");
            return false;
        }

        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            // Should be unreachable — TurnstileSettingsValidator fails startup on an empty SecretKey —
            // but never fall open if it somehow is.
            logger.LogError("Turnstile verification rejected: no SecretKey is configured.");
            return false;
        }

        var fields = new Dictionary<string, string>
        {
            ["secret"] = secretKey,
            ["response"] = token,
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            fields["remoteip"] = remoteIp;
        }

        TurnstileSiteverifyResponse? result;
        try
        {
            using var content = new FormUrlEncodedContent(fields);
            using var response = await httpClient.PostAsync(SiteverifyPath, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile siteverify returned HTTP {StatusCode}.", (int)response.StatusCode);
                return false;
            }

            result = await response.Content.ReadFromJsonAsync<TurnstileSiteverifyResponse>(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Turnstile siteverify request timed out.");
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Turnstile siteverify request failed at the network/TLS layer.");
            return false;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Turnstile siteverify returned an unparsable response body.");
            return false;
        }

        if (result is null)
        {
            logger.LogWarning("Turnstile siteverify returned an empty response body.");
            return false;
        }

        if (!result.Success)
        {
            // Cloudflare's own defined error-code strings (e.g. "invalid-input-response",
            // "timeout-or-duplicate") — never the token or secret, safe to log.
            logger.LogInformation(
                "Turnstile verification failed: {ErrorCodes}",
                result.ErrorCodes is { Length: > 0 } codes ? string.Join(',', codes) : "(none reported)");
            return false;
        }

        if (!string.IsNullOrEmpty(expectedAction) && !string.Equals(result.Action, expectedAction, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Turnstile verification rejected: expected action {ExpectedAction} but token was issued for {ActualAction}.",
                expectedAction, result.Action ?? "(none)");
            return false;
        }

        var expectedHostname = options.Value.ExpectedHostname;
        if (!string.IsNullOrEmpty(expectedHostname) && !string.Equals(result.Hostname, expectedHostname, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Turnstile verification rejected: expected hostname {ExpectedHostname} but token was issued for {ActualHostname}.",
                expectedHostname, result.Hostname ?? "(none)");
            return false;
        }

        return true;
    }

    private sealed class TurnstileSiteverifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; init; }
    }
}
