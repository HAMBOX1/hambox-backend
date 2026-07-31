using HAMBOX.Modules.Catalog.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>Same shape as Identity's <c>PlatformSettingsSecretProtector</c>, scoped to Catalog package passwords.</summary>
internal sealed class CatalogPackageSecretProtector(IDataProtectionProvider dataProtectionProvider)
    : ICatalogPackageSecretProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("HAMBOX.Catalog.PackagePasswords.v1");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}
