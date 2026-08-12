using HAMBOX.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Pass-through protector so encrypted-at-rest columns (license keys, digital codes)
/// don't need real Data Protection wiring in unit tests.</summary>
internal sealed class FakeCodeProtector : ICodeProtector
{
    public string Protect(string plainText) => plainText;

    public string Unprotect(string cipherText) => cipherText;
}
