using Microsoft.AspNetCore.DataProtection;

namespace HAMBOX.Modules.Identity.Infrastructure.PlatformSettings;

internal interface IPlatformSettingsSecretProtector
{
    string Protect(string plainText);

    string Unprotect(string cipherText);
}

internal sealed class PlatformSettingsSecretProtector(IDataProtectionProvider dataProtectionProvider)
    : IPlatformSettingsSecretProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("HAMBOX.PlatformSettings.Secrets.v1");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}
