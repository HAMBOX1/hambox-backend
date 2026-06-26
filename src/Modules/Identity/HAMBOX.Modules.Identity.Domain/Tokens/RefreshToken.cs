using System.Security.Cryptography;
using System.Text;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Tokens;

/// <summary>
/// Represents a refresh token issued to a user for obtaining new access tokens.
/// </summary>
public sealed class RefreshToken : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshToken"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        UserId = userId;
        Token = tokenHash;
        ExpiresOnUtc = expiresOnUtc;
    }

    /// <summary>
    /// Gets the identifier of the user this token belongs to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the SHA-256 hash of the refresh token value.
    /// </summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the date and time, in UTC, when the token expires.
    /// </summary>
    public DateTimeOffset ExpiresOnUtc { get; private set; }

    /// <summary>
    /// Gets the date and time, in UTC, when the token was revoked.
    /// A value of <see langword="null"/> indicates the token has not been revoked.
    /// </summary>
    public DateTimeOffset? RevokedOnUtc { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the token has expired.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOnUtc;

    /// <summary>
    /// Gets a value indicating whether the token has been revoked.
    /// </summary>
    public bool IsRevoked => RevokedOnUtc.HasValue;

    /// <summary>
    /// Gets a value indicating whether the token is still active (not expired and not revoked).
    /// </summary>
    public bool IsActive => !IsExpired && !IsRevoked;

    /// <summary>
    /// Issues a new refresh token, persisting only the SHA-256 hash of the plaintext value.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="plaintextToken">The plaintext token value returned to the client.</param>
    /// <param name="expiresOnUtc">The expiration date and time in UTC.</param>
    /// <returns>The persisted token entity and the plaintext value for the client.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the user identifier is empty or the token is null or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the expiration is not in the future.</exception>
    public static (RefreshToken Token, string Plaintext) Issue(
        Guid userId,
        string plaintextToken,
        DateTimeOffset expiresOnUtc)
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

        var token = new RefreshToken(
            Guid.NewGuid(),
            userId,
            ComputeHash(plaintextToken),
            expiresOnUtc);

        return (token, plaintextToken);
    }

    /// <summary>
    /// Computes the stored lookup hash for a plaintext refresh token.
    /// </summary>
    /// <param name="plaintextToken">The plaintext token supplied by the client.</param>
    /// <returns>The SHA-256 hash used for database lookups.</returns>
    public static string GetLookupHash(string plaintextToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        return ComputeHash(plaintextToken);
    }

    /// <summary>
    /// Determines whether the supplied plaintext token matches this persisted token hash.
    /// </summary>
    /// <param name="plaintextToken">The plaintext token supplied by the client.</param>
    /// <returns><see langword="true"/> when the token matches; otherwise <see langword="false"/>.</returns>
    public bool Matches(string plaintextToken) => Token == GetLookupHash(plaintextToken);

    /// <summary>
    /// Revokes the refresh token, preventing further use.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the token is already revoked.</exception>
    public void Revoke()
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("Token is already revoked.");
        }

        RevokedOnUtc = DateTimeOffset.UtcNow;
    }

    private static string ComputeHash(string plaintextToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(hashBytes);
    }
}
