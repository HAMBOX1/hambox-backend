using System.Security.Cryptography;
using System.Text;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Tokens;

/// <summary>
/// Represents a pending admin login OTP challenge after password validation.
/// </summary>
public sealed class AdminLoginChallenge : Entity
{
    private AdminLoginChallenge()
    {
    }

    private AdminLoginChallenge(
        Guid id,
        Guid userId,
        string codeHash,
        DateTimeOffset expiresOnUtc,
        string ipAddress,
        string userAgent)
        : base(id)
    {
        UserId = userId;
        CodeHash = codeHash;
        ExpiresOnUtc = expiresOnUtc;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        AttemptCount = 0;
    }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresOnUtc { get; private set; }

    public DateTimeOffset? UsedOnUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastResendOnUtc { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public string IpAddress { get; private set; } = string.Empty;

    public string UserAgent { get; private set; } = string.Empty;

    public bool IsUsed => UsedOnUtc.HasValue;

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOnUtc;

    public bool IsLocked => LockedUntilUtc.HasValue && LockedUntilUtc > DateTimeOffset.UtcNow;

    public bool IsActive => !IsUsed && !IsExpired && !IsLocked;

    public static AdminLoginChallenge Create(
        Guid userId,
        string plaintextCode,
        DateTimeOffset expiresOnUtc,
        string ipAddress,
        string userAgent)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresOnUtc), "Expiration must be in the future.");
        }

        return new AdminLoginChallenge(
            Guid.NewGuid(),
            userId,
            ComputeHash(plaintextCode),
            expiresOnUtc,
            ipAddress,
            userAgent);
    }

    public void RecordResend(string plaintextCode, DateTimeOffset expiresOnUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot resend OTP for an inactive challenge.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextCode);

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresOnUtc), "Expiration must be in the future.");
        }

        CodeHash = ComputeHash(plaintextCode);
        ExpiresOnUtc = expiresOnUtc;
        AttemptCount = 0;
        LastResendOnUtc = DateTimeOffset.UtcNow;
    }

    public bool VerifyCode(string plaintextCode)
    {
        if (!IsActive)
        {
            return false;
        }

        return CodeHash == ComputeHash(plaintextCode);
    }

    public void RecordFailedAttempt(int maxAttempts, int lockoutMinutes)
    {
        AttemptCount++;

        if (AttemptCount >= maxAttempts)
        {
            LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(lockoutMinutes);
        }
    }

    public void MarkUsed()
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("Challenge has already been used.");
        }

        UsedOnUtc = DateTimeOffset.UtcNow;
    }

    public static string ComputeHash(string plaintextCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextCode);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextCode));
        return Convert.ToHexString(hashBytes);
    }
}
