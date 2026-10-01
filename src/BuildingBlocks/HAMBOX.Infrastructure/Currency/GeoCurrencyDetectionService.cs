using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Best-effort "what currency should a brand-new visitor see" lookup, based on a free IP-geolocation
/// API (ipwho.is — no API key, no account required). Never blocks longer than a couple seconds and
/// always degrades to USD on any failure/timeout: this only picks the storefront's starting point —
/// the currency switcher lets the visitor override it immediately, and the choice is never persisted
/// server-side from this lookup alone.
/// <para>
/// ipapi.co (the original choice here) started returning a Cloudflare JS challenge instead of JSON
/// for server-to-server calls — ipwho.is was verified live to return plain JSON with no such gate.
/// </para>
/// </summary>
public sealed class GeoCurrencyDetectionService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<GeoCurrencyDetectionService> logger)
{
    private static readonly IReadOnlyDictionary<string, string> CountryCurrencyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EG"] = "EGP",
            ["SA"] = "SAR",
        };

    private const string DefaultCurrency = "USD";
    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan FallbackCacheDuration = TimeSpan.FromMinutes(10);

    public async Task<string> DetectCurrencyAsync(IPAddress? clientIp, CancellationToken cancellationToken = default)
    {
        if (clientIp is null || IsPrivateOrLoopback(clientIp))
        {
            return DefaultCurrency;
        }

        var ip = clientIp.ToString();
        var cacheKey = $"hambox.geo-currency:{ip}";
        if (cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            return cached;
        }

        var (currency, cacheDuration) = await ResolveAsync(ip, cancellationToken);
        cache.Set(cacheKey, currency, cacheDuration);
        return currency;
    }

    private async Task<(string Currency, TimeSpan CacheDuration)> ResolveAsync(string ip, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

            var response = await httpClient.GetFromJsonAsync<IpWhoIsResponse>(
                $"https://ipwho.is/{ip}?fields=success,country_code", timeoutCts.Token);

            if (response is null || !response.Success || string.IsNullOrWhiteSpace(response.CountryCode))
            {
                return (DefaultCurrency, FallbackCacheDuration);
            }

            var currency = CountryCurrencyMap.TryGetValue(response.CountryCode, out var mapped) ? mapped : DefaultCurrency;
            return (currency, SuccessCacheDuration);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Geo-currency lookup failed for IP {Ip} — defaulting to {Default}.", ip, DefaultCurrency);
            // Short cache on failure so a transient outage self-heals on the next visitor from this
            // IP soon after, instead of pinning everyone behind it to USD for the full success TTL.
            return (DefaultCurrency, FallbackCacheDuration);
        }
    }

    private sealed class IpWhoIsResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; } = true;

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; init; }
    }

    private static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] switch
        {
            10 => true,
            127 => true,
            172 when bytes[1] is >= 16 and <= 31 => true,
            192 when bytes[1] == 168 => true,
            _ => false,
        };
    }
}
