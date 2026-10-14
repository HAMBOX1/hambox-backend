using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Security;

/// <summary>
/// Authorization matrix over the REAL route table of the API host: every admin-area endpoint must be gated, and
/// every permission policy an endpoint names must be a permission that actually exists. A regression here is an
/// admin route that silently became public, or a typo that makes a route unreachable for everyone but Owner.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class AdminEndpointAuthorizationMatrixTests : IAsyncLifetime
{
    // Admin-portal sign-in has to be reachable without a session; everything else under /admin must not be.
    private static readonly string[] PublicAdminPathFragments =
    [
        "/auth/",
    ];

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.InitializeIdentitySchemaAsync();

    public async Task DisposeAsync()
    {
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private IReadOnlyList<RouteEndpoint> AllEndpoints()
    {
        _ = _factory.CreateClient(); // forces the host (and its route table) to be built
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        return dataSource.Endpoints.OfType<RouteEndpoint>().ToList();
    }

    private IReadOnlyList<RouteEndpoint> AdminEndpoints() =>
        AllEndpoints()
            .Where(e => e.RoutePattern.RawText is { } raw
                && raw.Contains("/admin", StringComparison.OrdinalIgnoreCase)
                && !PublicAdminPathFragments.Any(p => raw.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    [Fact]
    public void EveryAdminEndpoint_RequiresAuthorization_AndIsNotAnonymous()
    {
        var endpoints = AdminEndpoints();
        Assert.NotEmpty(endpoints);

        var open = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null
                || !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(e => $"{string.Join(",", e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])} {e.RoutePattern.RawText}")
            .OrderBy(x => x)
            .ToList();

        Assert.True(open.Count == 0, "Admin endpoints reachable without authorization:\n" + string.Join("\n", open));
    }

    [Fact]
    public void EveryPermissionPolicyNamedByAnEndpoint_IsARealPermission()
    {
        var known = PermissionConstants.All.ToHashSet(StringComparer.Ordinal);

        var unknown = AllEndpoints()
            .SelectMany(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(a => a.Policy)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .SelectMany(p => p!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(name => (Route: e.RoutePattern.RawText, Policy: name))))
            .Where(x => x.Policy.Contains('.') && !known.Contains(x.Policy))
            .Select(x => $"{x.Policy} on {x.Route}")
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        Assert.True(unknown.Count == 0, "Policies that are not registered permissions:\n" + string.Join("\n", unknown));
    }

    [Fact]
    public void RevealingALicenseKey_RequiresItsOwnPermission_NotJustOrdersView()
    {
        var reveal = AllEndpoints().Single(e =>
            e.RoutePattern.RawText!.Contains("license-keys/{licenseKeyId:guid}/reveal", StringComparison.OrdinalIgnoreCase));

        var policies = reveal.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();

        Assert.Contains(PermissionConstants.Orders.RevealLicenseKeys, policies);
        Assert.DoesNotContain(PermissionConstants.Orders.View, policies);
    }

    [Fact]
    public void PresetStaffRoles_OnlyReferenceRealPermissions_AndNeverGrantPlaintextReveal()
    {
        var known = PermissionConstants.All.ToHashSet(StringComparer.Ordinal);

        Assert.Equal(4, PresetRoleDefinitions.All.Count);

        foreach (var preset in PresetRoleDefinitions.All)
        {
            Assert.NotEmpty(preset.Permissions);
            Assert.All(preset.Permissions, name => Assert.Contains(name, known));
            Assert.DoesNotContain(PermissionConstants.Orders.RevealLicenseKeys, preset.Permissions);
            Assert.DoesNotContain(PermissionConstants.Catalog.Inventory.RevealCodes, preset.Permissions);
        }

        var auditor = PresetRoleDefinitions.All.Single(r => r.Name == "Read-only Auditor");
        Assert.All(auditor.Permissions, name =>
            Assert.True(
                name.EndsWith(".View", StringComparison.Ordinal) || name.Contains("View", StringComparison.Ordinal),
                $"Auditor must be read-only but holds {name}"));
    }
}
