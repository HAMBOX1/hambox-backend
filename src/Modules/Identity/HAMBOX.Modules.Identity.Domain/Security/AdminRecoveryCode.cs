using System.Security.Cryptography;
using System.Text;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// A one-time recovery code for an administrator who lost their authenticator. Only the SHA-256 hash is stored
/// (the plaintext is shown exactly once, at generation); a code is consumed by use and can never be replayed.
/// </summary>
public sealed class AdminRecoveryCode : Entity
{
    private AdminRecoveryCode()
    {
    }

    private AdminRecoveryCode(Guid id, Guid userId, string codeHash)
        : base(id)
    {
        UserId = userId;
        CodeHash = codeHash;
    }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset? UsedOnUtc { get; private set; }

    public bool IsUsed => UsedOnUtc.HasValue;

    public static AdminRecoveryCode Create(Guid userId, string plaintextCode)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        return new AdminRecoveryCode(Guid.NewGuid(), userId, ComputeHash(plaintextCode));
    }

    public void MarkUsed()
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("Recovery code has already been used.");
        }

        UsedOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Case- and separator-insensitive, so "abcd-1234", "ABCD1234" and " abcd 1234 " are the same code.</summary>
    public static string ComputeHash(string plaintextCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextCode);

        var normalized = new string(plaintextCode.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
