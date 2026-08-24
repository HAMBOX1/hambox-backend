using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Tokens;

/// <summary>
/// Represents a token issued to reset a user's password.
/// </summary>
public sealed class PasswordResetToken : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordResetToken"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private PasswordResetToken()
    {
    }

    private PasswordResetToken(Guid id, Guid userId, string token, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        UserId = userId;
        Token = token;
        ExpiresOnUtc = expiresOnUtc;
    }

    /// <summary>
    /// Gets the identifier of the user this token was issued to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the token value.
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
    /// Creates a new password reset token.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="token">The token value.</param>
    /// <param name="expiresOnUtc">The expiration date and time in UTC.</param>
    /// <returns>A new <see cref="PasswordResetToken"/> instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the user identifier is empty or the token is null or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the expiration is not in the future.</exception>
    public static PasswordResetToken Create(Guid userId, string token, DateTimeOffset expiresOnUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresOnUtc), "Expiration must be in the future.");
        }

        return new PasswordResetToken(Guid.NewGuid(), userId, token, expiresOnUtc);
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
