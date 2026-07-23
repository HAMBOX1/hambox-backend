using System.Security.Cryptography;
using HAMBOX.Modules.Catalog.Application.Abstractions;

namespace HAMBOX.Modules.Catalog.Infrastructure.Packaging;

/// <summary>
/// Self-contained, portable encryption for exported package contents (digital codes and/or the
/// whole package). Deliberately independent of ASP.NET Core Data Protection — see
/// <see cref="IPackageCryptoService"/>'s doc comment for why. Layout:
/// <c>[16-byte salt][12-byte nonce][16-byte tag][ciphertext]</c>, key derived via PBKDF2-SHA256
/// (200k iterations) from the caller-supplied password.
/// </summary>
internal sealed class PackageCryptoService : IPackageCryptoService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 200_000;

    public byte[] Encrypt(byte[] plaintext, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var result = new byte[SaltSize + NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, SaltSize + NonceSize + TagSize, ciphertext.Length);
        return result;
    }

    public byte[] Decrypt(byte[] ciphertext, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (ciphertext.Length < SaltSize + NonceSize + TagSize)
        {
            throw new InvalidDataException("Encrypted payload is too short to be valid.");
        }

        var salt = ciphertext[..SaltSize];
        var nonce = ciphertext[SaltSize..(SaltSize + NonceSize)];
        var tag = ciphertext[(SaltSize + NonceSize)..(SaltSize + NonceSize + TagSize)];
        var cipher = ciphertext[(SaltSize + NonceSize + TagSize)..];

        var key = DeriveKey(password, salt);
        var plaintext = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        try
        {
            aes.Decrypt(nonce, cipher, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new UnauthorizedAccessException("The package password is incorrect or the file is corrupted.", ex);
        }

        return plaintext;
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
}
