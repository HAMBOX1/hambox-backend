using System.Security.Cryptography;
using System.Text;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Tokens;

/// <summary>
/// A pending e-mailed one-time code guarding one sensitive customer action on one object (for example
/// revealing license key X). Only the hash of the code is stored; it expires, is single-use, and locks after
/// too many wrong attempts.
/// </summary>
public sealed class CustomerActionChallenge : Entity
{
    private CustomerActionChallenge()
    {
    }

    private CustomerActionChallenge(
        Guid id, Guid userId, string purpose, string scopeKey, string codeHash, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        UserId = userId;
        Purpose = purpose;
        ScopeKey = scopeKey;
        CodeHash = codeHash;
        ExpiresOnUtc = expiresOnUtc;
        LastSentOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }

    public string Purpose { get; private set; } = string.Empty;

    public string ScopeKey { get; private set; } = string.Empty;

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresOnUtc { get; private set; }

    public DateTimeOffset LastSentOnUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset? UsedOnUtc { get; private set; }

    public bool IsUsed => UsedOnUtc.HasValue;

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOnUtc;

    public bool IsLocked => LockedUntilUtc.HasValue && LockedUntilUtc > DateTimeOffset.UtcNow;

    public static CustomerActionChallenge Create(
        Guid userId, string purpose, string scopeKey, string plaintextCode, DateTimeOffset expiresOnUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextCode);

        return new CustomerActionChallenge(
            Guid.NewGuid(), userId, purpose, scopeKey, ComputeHash(plaintextCode), expiresOnUtc);
    }

    public bool Matches(string plaintextCode) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(CodeHash), Encoding.ASCII.GetBytes(ComputeHash(plaintextCode)));

    public void RecordFailedAttempt(int maxAttempts, int lockoutMinutes)
    {
        AttemptCount++;
        if (AttemptCount >= maxAttempts)
        {
            LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(lockoutMinutes);
        }
    }

    public void MarkUsed() => UsedOnUtc = DateTimeOffset.UtcNow;

    /// <summary>Retires a still-open challenge when a newer one replaces it.</summary>
    public void Supersede() => UsedOnUtc ??= DateTimeOffset.UtcNow;

    private static string ComputeHash(string plaintextCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextCode.Trim())));
}
