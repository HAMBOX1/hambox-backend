using System.Security.Cryptography;
using System.Text;

namespace HAMBOX.Modules.Identity.Domain.Sessions;

/// <summary>
/// Computes a stable, opaque identifier for the client behind a login attempt, used to recognize
/// a recurring device across sessions (<see cref="TrustedDevice"/>) without any client-side
/// cookie or fingerprinting script.
/// </summary>
/// <remarks>
/// ponytail: derived from the User-Agent string alone, so two installs with a byte-identical UA
/// (e.g. two default browser installs of the same OS/version) collapse to one fingerprint —
/// acceptable since this is investigative/trust-labeling data only, never an auth gate. Upgrade
/// path if stronger per-install identity is ever needed: a persisted client-side device-id cookie
/// set on first login.
/// </remarks>
public static class DeviceFingerprint
{
    public static string Compute(string? userAgent)
    {
        var normalized = (userAgent ?? string.Empty).Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }
}
