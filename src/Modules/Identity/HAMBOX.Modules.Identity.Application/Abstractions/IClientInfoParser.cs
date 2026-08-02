namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Parses client metadata from HTTP context values.
/// </summary>
public interface IClientInfoParser
{
    (string? BrowserName, string? OsName, string? DeviceName) ParseUserAgent(string userAgent);
}
