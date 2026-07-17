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
