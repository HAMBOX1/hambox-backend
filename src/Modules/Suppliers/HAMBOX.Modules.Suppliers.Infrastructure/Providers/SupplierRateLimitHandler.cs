using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers;

/// <summary>
/// Shared, per-provider rate-limit memory (audit R41): when a supplier answers 429 it tells us how long to back off
/// (<c>Retry-After</c>, else a default), and until then HAMBOX sends it nothing — so a burst of orders cannot keep
/// hammering a supplier that already asked us to slow down.
/// </summary>
internal sealed class SupplierRateLimitState
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _blockedUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextSlot = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _time;

    public SupplierRateLimitState(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    public DateTimeOffset Now => _time.GetUtcNow();

    /// <summary>How long the provider is still in cool-down, or null when requests may go out.</summary>
    public TimeSpan? CooldownRemaining(string providerKey)
    {
        if (_blockedUntil.TryGetValue(providerKey, out var until) && until > Now)
        {
            return until - Now;
        }

        return null;
    }

    public void Block(string providerKey, TimeSpan duration)
    {
        var until = Now + duration;
        _blockedUntil.AddOrUpdate(providerKey, until, (_, existing) => existing > until ? existing : until);
    }

    /// <summary>
    /// Reserves the next send slot so requests to one provider are spaced at least <paramref name="minInterval"/>
    /// apart; returns how long the caller must wait before sending.
    /// </summary>
    public TimeSpan ReserveSlot(string providerKey, TimeSpan minInterval)
    {
        if (minInterval <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var now = Now;
        var slot = _nextSlot.AddOrUpdate(
            providerKey,
            now + minInterval,
            (_, previous) => (previous > now ? previous : now) + minInterval);

        var sendAt = slot - minInterval;
        return sendAt > now ? sendAt - now : TimeSpan.Zero;
    }
}

internal sealed class SupplierRateLimitOptions
{
    public const string SectionName = "SupplierRateLimits";

    /// <summary>Highest sustained request rate per supplier. 0 disables spacing.</summary>
    public double MaxRequestsPerSecond { get; set; } = 5;

    /// <summary>Back-off used when a 429 carries no usable Retry-After.</summary>
    public int DefaultCooldownSeconds { get; set; } = 60;

    /// <summary>A supplier cannot make us stay silent for longer than this.</summary>
    public int MaxCooldownSeconds { get; set; } = 900;
}

/// <summary>Outermost handler of every supplier client: spaces outgoing calls and honours 429 / Retry-After.</summary>
internal sealed class SupplierRateLimitHandler(
    string providerKey,
    SupplierRateLimitState state,
    SupplierRateLimitOptions fallbackOptions,
    ILogger<SupplierRateLimitHandler> logger,
    Func<CancellationToken, Task<SupplierRateLimitOptions?>>? optionsSource = null) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var options = fallbackOptions;
        if (optionsSource is not null)
        {
            try
            {
                options = await optionsSource(cancellationToken) ?? fallbackOptions;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Settings unavailable: keep the configured defaults rather than failing a supplier call.
            }
        }

        if (state.CooldownRemaining(providerKey) is { } remaining)
        {
            // Answer locally with the same 429 the supplier gave: every provider already maps it to
            // "provider unavailable, try later", and nothing reaches the supplier while it asked for quiet.
            return CooldownResponse(remaining);
        }

        var spacing = options.MaxRequestsPerSecond > 0
            ? TimeSpan.FromSeconds(1d / options.MaxRequestsPerSecond)
            : TimeSpan.Zero;
        var wait = state.ReserveSlot(providerKey, spacing);
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, cancellationToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == (HttpStatusCode)429)
        {
            var cooldown = ResolveCooldown(response.Headers.RetryAfter, state.Now, options);
            state.Block(providerKey, cooldown);
            logger.LogWarning(
                "{Provider} answered 429; pausing all calls to it for {Seconds:N0}s (Retry-After honoured).",
                providerKey, cooldown.TotalSeconds);
        }

        return response;
    }

    internal static TimeSpan ResolveCooldown(RetryConditionHeaderValue? retryAfter, DateTimeOffset now, SupplierRateLimitOptions options)
    {
        var fallback = TimeSpan.FromSeconds(Math.Max(1, options.DefaultCooldownSeconds));
        var max = TimeSpan.FromSeconds(Math.Max(1, options.MaxCooldownSeconds));

        TimeSpan? requested = null;
        if (retryAfter?.Delta is { } delta)
        {
            requested = delta;
        }
        else if (retryAfter?.Date is { } date)
        {
            requested = date - now;
        }

        var cooldown = requested is { } r && r > TimeSpan.Zero ? r : fallback;
        return cooldown > max ? max : cooldown;
    }

    private static HttpResponseMessage CooldownResponse(TimeSpan remaining)
    {
        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var response = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent(
                $$"""{"error":"RateLimited","message":"HAMBOX is waiting out the supplier's rate limit ({{seconds}}s remaining)."}""",
                Encoding.UTF8,
                "application/json"),
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        return response;
    }
}

/// <summary>Reads the admin-editable supplier throttling values (Platform settings → Retry policies) per call.</summary>
internal static class SupplierRateLimitOptionsSource
{
    public static Func<CancellationToken, Task<SupplierRateLimitOptions?>> From(IServiceProvider services)
    {
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var configured = services.GetRequiredService<IOptions<SupplierRateLimitOptions>>().Value;

        return async cancellationToken =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetService<HAMBOX.Application.Abstractions.IPlatformSettingsProvider>();
            if (settings is null)
            {
                return null;
            }

            var payload = await settings.GetAsync<HAMBOX.Application.PlatformSettings.RetryPoliciesSettingsPayload>(
                HAMBOX.Application.PlatformSettings.PlatformSettingsCategoryKeys.RetryPolicies, cancellationToken);

            return new SupplierRateLimitOptions
            {
                MaxRequestsPerSecond = payload.SupplierMaxRequestsPerSecond >= 0 ? payload.SupplierMaxRequestsPerSecond : configured.MaxRequestsPerSecond,
                DefaultCooldownSeconds = payload.SupplierDefaultCooldownSeconds > 0 ? payload.SupplierDefaultCooldownSeconds : configured.DefaultCooldownSeconds,
                MaxCooldownSeconds = payload.SupplierMaxCooldownSeconds > 0 ? payload.SupplierMaxCooldownSeconds : configured.MaxCooldownSeconds,
            };
        };
    }
}
