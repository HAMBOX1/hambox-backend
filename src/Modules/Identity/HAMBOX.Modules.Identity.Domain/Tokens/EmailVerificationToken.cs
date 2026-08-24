using System.Security.Cryptography;
using System.Text;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Tokens;

/// <summary>
/// Represents a token issued to verify a user's email address.
/// </summary>
public sealed class EmailVerificationToken : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailVerificationToken"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private EmailVerificationToken()
    {
    }

    private EmailVerificationToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        UserId = userId;
        Token = tokenHash;
        ExpiresOnUtc = expiresOnUtc;
    }

    /// <summary>
    /// Gets the identifier of the user this token was issued to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the SHA-256 hash of the plaintext token value. The plaintext is never persisted —
    /// only whoever received it (e.g. via the verification email) can produce a matching lookup hash.
    /// </summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the date and time, in UTC, when the token expires.
    /// </summary>
    public DateTimeOffset ExpiresOnUtc { get; private set; }

    /// <summary>
    /// Gets the date and time, in UTC, when the token was used.
    /// A value of <see langword="null"/> indicates the token has not been used.
    /// </summary>
    public DateTimeOffset? UsedOnUtc { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the token has expired.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOnUtc;

    /// <summary>
    /// Gets a value indicating whether the token has been used.
    /// </summary>
    public bool IsUsed => UsedOnUtc.HasValue;

    /// <summary>
    /// Creates a new email verification token. Only the SHA-256 hash of <paramref name="plaintextToken"/>
    /// is persisted; the caller is responsible for delivering the plaintext value (e.g. via email).
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="plaintextToken">The plaintext token value to be delivered to the user.</param>
    /// <param name="expiresOnUtc">The expiration date and time in UTC.</param>
    /// <returns>A new <see cref="EmailVerificationToken"/> instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the user identifier is empty or the token is null or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the expiration is not in the future.</exception>
    public static EmailVerificationToken Create(Guid userId, string plaintextToken, DateTimeOffset expiresOnUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresOnUtc), "Expiration must be in the future.");
        }

        return new EmailVerificationToken(Guid.NewGuid(), userId, GetLookupHash(plaintextToken), expiresOnUtc);
    }

    /// <summary>
    /// Computes the stored lookup hash for a plaintext email verification token.
    /// </summary>
    /// <param name="plaintextToken">The plaintext token supplied by the client.</param>
    /// <returns>The SHA-256 hash used for database lookups.</returns>
    public static string GetLookupHash(string plaintextToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Marks the token as used, preventing further use.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the token has already been used or has expired.
    /// </exception>
    public void MarkAsUsed()
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("Token has already been used.");
        }

        if (IsExpired)
        {
            throw new InvalidOperationException("Token has expired.");
        }

        UsedOnUtc = DateTimeOffset.UtcNow;
    }
}
