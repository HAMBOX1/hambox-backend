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

    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresOnUtc,
        string authContext,
        Guid sessionId,
        bool isPersistent)
        : base(id)
    {
        UserId = userId;
        Token = tokenHash;
        ExpiresOnUtc = expiresOnUtc;
        AuthContext = authContext;
        SessionId = sessionId;
        IsPersistent = isPersistent;
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
    /// Gets the auth context this refresh token belongs to (customer or admin).
    /// </summary>
    public string AuthContext { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the linked user session identifier.
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Gets the hash of the replacement token when this token was rotated.
    /// </summary>
    public string? ReplacedByTokenHash { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this token was issued under "Remember Me"
    /// and should keep using the extended lifetime on each rotation.
    /// </summary>
    public bool IsPersistent { get; private set; }

    /// <summary>
    /// SQL Server <c>rowversion</c> concurrency token. Without this, two truly-concurrent refresh
    /// requests presenting the same still-active token both read the row, both pass the
    /// <see cref="IsActive"/> check, and both successfully revoke+rotate independently — replaying a
    /// stolen-but-not-yet-rotated cookie concurrently with the legitimate holder would silently mint
    /// two live sessions instead of the second request failing. EF's SaveChangesAsync includes this
    /// column in every UPDATE's WHERE clause, so the loser of the race gets a
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> instead of a second
    /// silently-successful rotation.
    /// </summary>
    public byte[]? RowVersion { get; private set; }

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
        DateTimeOffset expiresOnUtc,
        string authContext,
        Guid sessionId,
        bool isPersistent = false)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(authContext);

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier is required.", nameof(sessionId));
        }

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresOnUtc), "Expiration must be in the future.");
        }

        var token = new RefreshToken(
            Guid.NewGuid(),
            userId,
            ComputeHash(plaintextToken),
            expiresOnUtc,
            authContext,
            sessionId,
            isPersistent);

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

    /// <summary>
    /// Marks this token as replaced during rotation to detect reuse.
    /// </summary>
    public void MarkReplacedBy(string newPlaintextToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlaintextToken);
        ReplacedByTokenHash = ComputeHash(newPlaintextToken);
    }

    /// <summary>
    /// Determines whether this token was already rotated and reused.
    /// </summary>
    public bool WasReused() => !string.IsNullOrEmpty(ReplacedByTokenHash);

    private static string ComputeHash(string plaintextToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(hashBytes);
    }
}
