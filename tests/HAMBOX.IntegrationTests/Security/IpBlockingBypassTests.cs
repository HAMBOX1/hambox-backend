using System.Net;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Security;

/// <summary>
/// Boots the real HAMBOX.API host (reusing <see cref="HamboxRateLimitWebApplicationFactory"/>) and
/// exercises the real <c>UseForwardedHeaders</c> → <c>SecurityEnforcementMiddleware</c> pipeline
/// against a blocked IP entry seeded through the real <see cref="BlockedIp"/> domain factory.
///
/// A raw <c>WebApplicationFactory</c> TestServer connection never has a real socket, so
/// <c>HttpContext.Connection.RemoteIpAddress</c> would otherwise be null on every request — useless
/// for exercising IP-keyed middleware. <see cref="SimulatedRemoteIpStartupFilter"/> is a test-only
/// <see cref="IStartupFilter"/> inserted at the very front of the pipeline (before
/// <c>UseForwardedHeaders</c> runs, exactly like it would for a real Kestrel connection) that reads a
/// test-only header and stamps <c>Connection.RemoteIpAddress</c> from it — simulating "this is the
/// address the raw socket connected from" without touching any production code or Program.cs.
///
/// Each test seeds its own blocked address so scenarios can't interfere with each other under
/// xUnit's default parallel-within-class execution (each fact still runs against the same shared
/// scratch database via <see cref="HamboxApiFactoryCollection"/> sequencing at the class level).
///
/// Findings this documents (2026-08-30 Security Center customer-blocking review, cross-checked
/// against a real `dotnet run` Kestrel instance — see report): the block on a given address is
/// bypassable by sending <c>X-Forwarded-For</c> for a DIFFERENT (allowed) address FROM that same
/// blocked address, whenever the blocked address is itself a source ASP.NET Core's
/// ForwardedHeadersMiddleware trusts as a proxy (loopback, by default, plus the Docker-bridge range
/// Program.cs adds) — because the middleware then evaluates the forwarded value instead of the real
/// one. The two control cases confirm the matching logic itself is correct: an untrusted source's
/// forwarded claims are ignored in both directions.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class IpBlockingBypassTests : IAsyncLifetime
{
    private const string SimulatedRemoteIpHeader = "X-Test-Simulated-RemoteIp";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        _client = CreateClientWithSimulatedIpSupport();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private HttpClient CreateClientWithSimulatedIpSupport()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Insert(0, ServiceDescriptor.Singleton<IStartupFilter>(new SimulatedRemoteIpStartupFilter()))));
        return factory.CreateClient();
    }

    private async Task SeedBlockedIpAsync(string cidrOrAddress)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.BlockedIps.Add(BlockedIp.Create(cidrOrAddress, $"integration-test-seed-{Guid.NewGuid():N}"));
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ISecurityBlocklistService>().InvalidateCache();
    }

    // Deliberately NOT "health" or a "swagger"-containing path: SecurityEnforcementMiddleware
    // explicitly exempts those two path substrings before the IP-block check ever runs. Any other
    // path — including one matching no endpoint — passes through the check first, since the
    // middleware runs ahead of routing; a request that clears the check falls through to a
    // harmless 404 rather than the middleware's own 403 short-circuit.
    private static HttpRequestMessage Request(string simulatedRemoteIp, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "sectest-probe-path");
        request.Headers.Add(SimulatedRemoteIpHeader, simulatedRemoteIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return request;
    }

    [Fact]
    public async Task BlockedIp_DirectConnection_NoForwardedHeader_IsRejected()
    {
        const string blocked = "198.51.100.77";
        await SeedBlockedIpAsync($"{blocked}/32");

        using var response = await _client.SendAsync(Request(blocked));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("IP_BLOCKED", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BlockedIp_ForwardedHeaderFromUntrustedExternalSource_CannotInjectAFalsePositive()
    {
        // Control case: an untrusted, non-proxy connecting peer (198.51.100.10, a public address —
        // not loopback, not in the Docker-bridge range Program.cs trusts) claims via X-Forwarded-For
        // to BE the blocked address. That claim must be ignored — an untrusted client must not be
        // able to get itself (or anyone) wrongly blocked by forging the header either.
        const string blocked = "198.51.100.77";
        const string untrustedPeer = "198.51.100.10";
        await SeedBlockedIpAsync($"{blocked}/32");

        using var response = await _client.SendAsync(Request(untrustedPeer, forwardedFor: blocked));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // cleared the check, fell through to routing
    }

    [Fact]
    public async Task BlockedIp_LoopbackSourceForwardedHeader_BypassesBlockOnThatSameSource()
    {
        // SECURITY FINDING (see conversation report, cross-checked live against a real `dotnet run`
        // Kestrel instance with the actual loopback client and a genuine bypass reproduced end to
        // end): ASP.NET Core's ForwardedHeadersMiddleware trusts loopback as a known proxy by
        // default. Program.cs only ever explicitly adds the Docker-bridge range (172.16.0.0/12) on
        // top of that default — it never narrows or removes the built-in loopback trust. A
        // connection whose real peer IS the blocked address, and IS also loopback (exactly what a
        // request reaching Kestrel directly, bypassing nginx, looks like), can set
        // X-Forwarded-For to any other address and have SecurityEnforcementMiddleware evaluate
        // that spoofed value instead of its own real (blocked) one.
        //
        // This test encodes the SECURE expectation (the block should hold regardless of a
        // client-controlled header) and is expected to FAIL until the trusted-proxy configuration
        // is tightened — see report. It is intentionally not adjusted to match the current
        // (insecure) behavior; that would defeat the point of a regression test.
        var blockedLoopback = IPAddress.IPv6Loopback.ToString(); // "::1" — the real peer IS the blocked address
        await SeedBlockedIpAsync($"{blockedLoopback}/128");

        using var response = await _client.SendAsync(Request(blockedLoopback, forwardedFor: "203.0.113.51"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class SimulatedRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(SimulatedRemoteIpHeader, out var simulated)
                    && IPAddress.TryParse(simulated.ToString(), out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                    context.Request.Headers.Remove(SimulatedRemoteIpHeader);
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}
