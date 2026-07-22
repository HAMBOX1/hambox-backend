namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Centralized encryption for secrets stored at rest (digital inventory codes, license keys).
/// Swap the DI registration to replace the underlying provider without touching callers.
/// </summary>
public interface ICodeProtector
{
    string Protect(string plainText);

    string Unprotect(string cipherText);
}

/// <summary>
/// Values produced by ASP.NET Core Data Protection's <c>IDataProtector.Protect(string)</c> always start
/// with "CfDJ" (the base64/url-safe encoding of the format's fixed magic header). Backfill migrators use
/// this to recognize already-encrypted values that the current key ring can no longer read (e.g. after a
/// key rotation) instead of blindly re-protecting the ciphertext, which would grow the stored value and
/// corrupt it further on every subsequent restart.
/// </summary>
public static class ProtectedValueFormat
{
    private const string CiphertextPrefix = "CfDJ";

    public static bool LooksAlreadyProtected(string value) =>
        value.StartsWith(CiphertextPrefix, StringComparison.Ordinal);
}
