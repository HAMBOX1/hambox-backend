using System.Net;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// Defense-in-depth only, per the DOT documentation's own note that notifications are sent from
/// 212.118.1.243. This is never the sole authentication mechanism — <c>HandleDotNotificationCommand</c>
/// still re-verifies every notification with DOT server-to-server before trusting it, since the
/// documented notification API has no signature or shared secret and IP alone is spoofable at
/// several points in a real network path.
/// </summary>
internal static class DotNotificationIpAllowlist
{
    private static readonly IReadOnlyCollection<IPAddress> AllowedIps =
    [
        IPAddress.Parse("212.118.1.243"),
    ];

    public static bool IsAllowed(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return false;
        }

        var normalized = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;
        return AllowedIps.Any(allowed => allowed.Equals(normalized));
    }
}
