using System.Net;
using System.Net.Http.Headers;
using HAMBOX.Modules.Suppliers.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>Audit R41: a supplier's 429 + Retry-After is honoured (nothing is sent until it passes), and sends are spaced.</summary>
public sealed class SupplierRateLimitTests
{
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public int Calls { get; private set; }

        public void Enqueue(HttpStatusCode status, TimeSpan? retryAfter = null)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
            if (retryAfter is { } value)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(value);
            }

            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static (HttpClient Client, ScriptedHandler Inner, SupplierRateLimitState State, ManualTimeProvider Time) Create(
        double maxRps = 0, int defaultCooldown = 60, int maxCooldown = 900)
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var state = new SupplierRateLimitState(time);
        var inner = new ScriptedHandler();
        var handler = new SupplierRateLimitHandler(
            "Bamboo",
            state,
            new SupplierRateLimitOptions { MaxRequestsPerSecond = maxRps, DefaultCooldownSeconds = defaultCooldown, MaxCooldownSeconds = maxCooldown },
            NullLogger<SupplierRateLimitHandler>.Instance)
        {
            InnerHandler = inner,
        };

        return (new HttpClient(handler) { BaseAddress = new Uri("https://api.supplier.test") }, inner, state, time);
    }

    [Fact]
    public async Task After429WithRetryAfter_NothingReachesTheSupplier_UntilItHasPassed()
    {
        var (client, inner, _, time) = Create();
        inner.Enqueue((HttpStatusCode)429, TimeSpan.FromSeconds(120));

        var first = await client.GetAsync("/orders");
        Assert.Equal((HttpStatusCode)429, first.StatusCode);
        Assert.Equal(1, inner.Calls);

        // During the cool-down HAMBOX answers locally with the same 429 — the supplier is not contacted.
        var during = await client.GetAsync("/orders");
        Assert.Equal((HttpStatusCode)429, during.StatusCode);
        Assert.NotNull(during.Headers.RetryAfter?.Delta);
        Assert.Equal(1, inner.Calls);

        time.Advance(TimeSpan.FromSeconds(121));
        var after = await client.GetAsync("/orders");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task A429WithoutRetryAfter_UsesTheDefaultCooldown()
    {
        var (client, inner, state, _) = Create(defaultCooldown: 45);
        inner.Enqueue((HttpStatusCode)429);

        await client.GetAsync("/orders");

        var remaining = state.CooldownRemaining("Bamboo");
        Assert.NotNull(remaining);
        Assert.Equal(45, Math.Round(remaining!.Value.TotalSeconds));
    }

    [Fact]
    public async Task ASupplierCannotMakeUsStaySilentLongerThanTheCap()
    {
        var (client, inner, state, _) = Create(maxCooldown: 600);
        inner.Enqueue((HttpStatusCode)429, TimeSpan.FromHours(6));

        await client.GetAsync("/orders");

        Assert.Equal(600, Math.Round(state.CooldownRemaining("Bamboo")!.Value.TotalSeconds));
    }

    [Fact]
    public async Task OnlyTheRateLimitedProviderIsPaused()
    {
        var (client, inner, state, _) = Create();
        inner.Enqueue((HttpStatusCode)429, TimeSpan.FromSeconds(60));

        await client.GetAsync("/orders");

        Assert.NotNull(state.CooldownRemaining("Bamboo"));
        Assert.Null(state.CooldownRemaining("Eneba"));
    }

    [Fact]
    public void RetryAfterGivenAsAnHttpDate_IsConvertedToADelay()
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var header = new RetryConditionHeaderValue(now.AddSeconds(90));

        var cooldown = SupplierRateLimitHandler.ResolveCooldown(header, now, new SupplierRateLimitOptions());

        Assert.Equal(TimeSpan.FromSeconds(90), cooldown);
    }

    [Fact]
    public void SendSlots_AreSpacedByTheConfiguredRate()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var state = new SupplierRateLimitState(time);
        var spacing = TimeSpan.FromMilliseconds(200); // 5 requests / second

        var first = state.ReserveSlot("Bamboo", spacing);
        var second = state.ReserveSlot("Bamboo", spacing);
        var third = state.ReserveSlot("Bamboo", spacing);

        Assert.Equal(TimeSpan.Zero, first);
        Assert.Equal(TimeSpan.FromMilliseconds(200), second);
        Assert.Equal(TimeSpan.FromMilliseconds(400), third);
    }
}
