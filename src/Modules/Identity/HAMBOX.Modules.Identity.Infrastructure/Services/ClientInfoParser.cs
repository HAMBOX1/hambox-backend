using HAMBOX.Modules.Identity.Application.Abstractions;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class ClientInfoParser : IClientInfoParser
{
    public (string? BrowserName, string? DeviceName) ParseUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return (null, null);
        }

        var ua = userAgent;

        string? browser = ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge"
            : ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            : ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            : ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : "Unknown";

        string? device = ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "Mobile"
            : ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ? "Tablet"
            : "Desktop";

        return (browser, device);
    }
}
