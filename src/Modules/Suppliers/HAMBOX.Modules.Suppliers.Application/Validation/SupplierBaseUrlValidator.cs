using System.Net;
using System.Net.Sockets;

namespace HAMBOX.Modules.Suppliers.Application.Validation;

/// <summary>
/// Rejects obviously dangerous supplier <c>BaseUrl</c> values before they're stored — HTTPS-only, no
/// loopback/private/link-local/reserved IP literals, no localhost-shaped hostnames. No real supplier
/// provider makes an outbound HTTP call yet (<c>ManualSupplierProvider</c> is a stub), so this is not
/// closing an active SSRF hole today; it exists so the first real HTTP-calling provider inherits a safe
/// default instead of the checks being an afterthought once one exists.
/// </summary>
/// <remarks>
/// This is static, literal-value validation only — it does not resolve DNS. A public hostname that
/// resolves to a private/loopback address at connect time (DNS rebinding, or a hostname that simply
/// changes its A record after being saved) is not caught here. Closing that gap requires validating the
/// resolved IP at the point an HTTP client actually connects (e.g. a <c>SocketsHttpHandler.ConnectCallback</c>),
/// which belongs in the real provider implementation, not this admin-input validator. Deliberately not
/// built here — no real provider exists yet to wire it into.
/// </remarks>
public static class SupplierBaseUrlValidator
{
    public static bool IsAllowed(string? url, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            errorMessage = "Base URL must be a valid absolute URL.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            errorMessage = "Base URL must use HTTPS.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            errorMessage = "Base URL must include a host.";
            return false;
        }

        if (IsDisallowedHostname(uri.Host))
        {
            errorMessage = "Base URL host is not allowed.";
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var ip) && IsDisallowedIpAddress(ip))
        {
            errorMessage = "Base URL must not point to a loopback, private, or link-local address.";
            return false;
        }

        return true;
    }

    private static bool IsDisallowedHostname(string host)
    {
        var normalized = host.Trim().ToLowerInvariant();

        return normalized is "localhost" or "0.0.0.0"
            || normalized.EndsWith(".localhost", StringComparison.Ordinal)
            || normalized.EndsWith(".local", StringComparison.Ordinal)
            // Cloud provider instance-metadata endpoints — never a legitimate supplier API host.
            || normalized is "metadata.google.internal" or "metadata.azure.com" or "metadata.aws.internal";
    }

    private static bool IsDisallowedIpAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] switch
            {
                0 => true, // 0.0.0.0/8 — "this network"
                10 => true, // 10.0.0.0/8 — private
                100 when b[1] is >= 64 and <= 127 => true, // 100.64.0.0/10 — carrier-grade NAT
                127 => true, // 127.0.0.0/8 — loopback (also caught by IsLoopback above)
                169 when b[1] == 254 => true, // 169.254.0.0/16 — link-local (incl. cloud metadata 169.254.169.254)
                172 when b[1] is >= 16 and <= 31 => true, // 172.16.0.0/12 — private
                192 when b[1] == 168 => true, // 192.168.0.0/16 — private
                _ => false,
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
        {
            return IsDisallowedIpAddress(ip.MapToIPv4());
        }

        return false;
    }
}
