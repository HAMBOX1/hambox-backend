using HAMBOX.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace HAMBOX.Infrastructure.Services;

internal sealed class DataProtectionCodeProtector(IDataProtectionProvider dataProtectionProvider) : ICodeProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("HAMBOX.Inventory.Codes.v1");

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}
