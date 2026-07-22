using System.Globalization;
using System.Security.Cryptography;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class MaintenanceBypassTokenIssuer(IDataProtectionProvider dataProtectionProvider)
    : IMaintenanceBypassTokenIssuer
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("HAMBOX.Maintenance.Bypass.v1");

    public (string Token, DateTimeOffset ExpiresOnUtc) Issue(TimeSpan validFor)
    {
        var expiresOnUtc = DateTimeOffset.UtcNow.Add(validFor);
        var token = _protector.Protect(expiresOnUtc.ToString("O", CultureInfo.InvariantCulture));
        return (token, expiresOnUtc);
    }

    public bool TryValidate(string token)
    {
        try
        {
            var payload = _protector.Unprotect(token);
            var expiresOnUtc = DateTimeOffset.Parse(payload, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return expiresOnUtc > DateTimeOffset.UtcNow;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
