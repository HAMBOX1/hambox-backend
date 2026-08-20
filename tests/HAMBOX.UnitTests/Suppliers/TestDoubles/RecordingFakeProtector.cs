using HAMBOX.Application.Abstractions;

namespace HAMBOX.UnitTests.Suppliers.TestDoubles;

/// <summary>
/// Unlike <see cref="FakeCodeProtector"/> (a pure pass-through), this actually transforms the value and
/// keeps a record of every plaintext it was asked to protect — for tests that need to prove a column is
/// genuinely round-tripped through <c>ICodeProtector</c> rather than stored/read verbatim.
/// </summary>
internal sealed class RecordingFakeProtector : ICodeProtector
{
    private const string Prefix = "ENC(";
    private const string Suffix = ")";

    public List<string> ProtectedValues { get; } = [];

    public string Protect(string plainText)
    {
        ProtectedValues.Add(plainText);
        return Prefix + plainText + Suffix;
    }

    public string Unprotect(string cipherText)
    {
        if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal) || !cipherText.EndsWith(Suffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Value was not produced by this protector.");
        }

        return cipherText[Prefix.Length..^Suffix.Length];
    }
}
