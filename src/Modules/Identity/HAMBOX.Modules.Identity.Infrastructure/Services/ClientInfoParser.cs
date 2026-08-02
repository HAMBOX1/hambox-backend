using HAMBOX.Modules.Identity.Application.Abstractions;
using UAParser;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class ClientInfoParser : IClientInfoParser
{
    private readonly Parser _parser = Parser.GetDefault();

    public (string? BrowserName, string? OsName, string? DeviceName) ParseUserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return (null, null, null);
        }

        var info = _parser.Parse(userAgent);

        var browser = FormatFamily(info.UserAgent.Family, info.UserAgent.Major);
        var os = FormatFamily(info.OS.Family, info.OS.Major);

        // ponytail: device *type* bucketing stays UA-token based (the same signal every
        // real-world detector uses to tell an Android phone from an Android tablet, since
        // neither UAParser nor the raw string exposes a dedicated category) rather than pulling
        // in a full device-detection library for one field.
        string device = userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
                         || userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase)
            ? "Tablet"
            : userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
                ? "Mobile"
                : "Desktop";

        return (browser, os, device);
    }

    private static string FormatFamily(string family, string? major)
    {
        if (string.IsNullOrWhiteSpace(family) || family == "Other")
        {
            return "Unknown";
        }

        return string.IsNullOrWhiteSpace(major) ? family : $"{family} {major}";
    }
}
