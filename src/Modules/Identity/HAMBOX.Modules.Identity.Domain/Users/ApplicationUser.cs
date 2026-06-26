using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Users.Events;

namespace HAMBOX.Modules.Identity.Domain.Users;

/// <summary>
/// Represents a user account in the system.
/// </summary>
public sealed class ApplicationUser : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationUser"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ApplicationUser()
    {
    }

    private ApplicationUser(
        Guid id,
        string email,
        string passwordHash,
        string firstName,
        string lastName)
        : base(id)
    {
        Email = email;
        NormalizedEmail = email.ToUpperInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Status = UserStatus.Pending;
        EmailConfirmed = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the normalized email address used for lookups and uniqueness checks.
    /// </summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the hashed password.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user's first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user's last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user's phone number.
    /// </summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>
    /// Gets the URL of the user's avatar image.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user's email address has been confirmed.
    /// </summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>
    /// Gets the current account status.
    /// </summary>
    public UserStatus Status { get; private set; }

    /// <summary>
    /// Gets the security stamp used to invalidate existing tokens when security-critical
    /// changes occur (e.g., password change, email confirmation, suspension).
    /// </summary>
    public string SecurityStamp { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the number of consecutive failed access attempts.
    /// </summary>
    public int AccessFailedCount { get; private set; }

    /// <summary>
    /// Gets the date and time, in UTC, when the account lockout ends.
    /// A value of <see langword="null"/> indicates the account is not locked out.
    /// </summary>
    public DateTimeOffset? LockoutEnd { get; private set; }

    /// <summary>
    /// Gets a value indicating whether two-factor authentication is enabled.
    /// </summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>
    /// Gets the user's preferred UI language (ISO 639-1 code, e.g. en, ar).
    /// </summary>
    public string PreferredLanguage { get; private set; } = "en";

    /// <summary>
    /// Gets the user's preferred display currency (ISO 4217 code).
    /// </summary>
    public string PreferredCurrency { get; private set; } = "USD";

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedOnUtc { get; private set; }

    /// <summary>
    /// Creates a new user account in <see cref="UserStatus.Pending"/> status.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="passwordHash">The hashed password.</param>
    /// <param name="firstName">The user's first name.</param>
    /// <param name="lastName">The user's last name.</param>
    /// <returns>A new <see cref="ApplicationUser"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when any required parameter is null or whitespace.</exception>
    public static ApplicationUser Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        return new ApplicationUser(Guid.NewGuid(), email, passwordHash, firstName, lastName);
    }

    /// <summary>
    /// Confirms the user's email address.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the email is already confirmed.</exception>
    public void ConfirmEmail()
    {
        if (EmailConfirmed)
        {
            throw new InvalidOperationException("Email is already confirmed.");
        }

        EmailConfirmed = true;
        SecurityStamp = Guid.NewGuid().ToString("N");

        RaiseDomainEvent(new UserEmailConfirmedDomainEvent(Id));
    }

    /// <summary>
    /// Activates the user account. Valid from <see cref="UserStatus.Pending"/>
    /// or <see cref="UserStatus.Suspended"/> status.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the user is already active or is blocked.
    /// </exception>
    public void Activate()
    {
        if (Status == UserStatus.Active)
        {
            throw new InvalidOperationException("User is already active.");
        }

        if (Status == UserStatus.Blocked)
        {
            throw new InvalidOperationException("A blocked user cannot be activated.");
        }

        Status = UserStatus.Active;
        AccessFailedCount = 0;
        LockoutEnd = null;

        RaiseDomainEvent(new UserActivatedDomainEvent(Id));
    }

    /// <summary>
    /// Suspends the user account. Only valid from <see cref="UserStatus.Active"/> status.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the user is not active.</exception>
    public void Suspend()
    {
        if (Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Only active users can be suspended.");
        }

        Status = UserStatus.Suspended;
        SecurityStamp = Guid.NewGuid().ToString("N");

        RaiseDomainEvent(new UserSuspendedDomainEvent(Id));
    }

    /// <summary>
    /// Permanently blocks the user account.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the user is already blocked.</exception>
    public void Block()
    {
        if (Status == UserStatus.Blocked)
        {
            throw new InvalidOperationException("User is already blocked.");
        }

        Status = UserStatus.Blocked;
        SecurityStamp = Guid.NewGuid().ToString("N");

        RaiseDomainEvent(new UserBlockedDomainEvent(Id));
    }

    /// <summary>
    /// Updates the user's profile information.
    /// </summary>
    /// <param name="firstName">The user's first name.</param>
    /// <param name="lastName">The user's last name.</param>
    /// <param name="phoneNumber">The user's phone number, or <see langword="null"/> to clear it.</param>
    /// <exception cref="ArgumentException">Thrown when a required parameter is null or whitespace.</exception>
    public void UpdateProfile(string firstName, string lastName, string? phoneNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
    }

    /// <summary>
    /// Updates the user's preferred UI language.
    /// </summary>
    /// <param name="preferredLanguage">A supported language code (en or ar).</param>
  /// <exception cref="ArgumentException">Thrown when the language code is not supported.</exception>
    public void SetPreferredLanguage(string preferredLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);

        var normalized = preferredLanguage.Split('-')[0].ToLowerInvariant();
        if (normalized is not ("en" or "ar"))
        {
            throw new ArgumentException("Preferred language must be 'en' or 'ar'.", nameof(preferredLanguage));
        }

        PreferredLanguage = normalized;
    }

    /// <summary>
    /// Updates the user's preferred display currency.
    /// </summary>
    /// <param name="preferredCurrency">A supported ISO 4217 currency code.</param>
    /// <exception cref="ArgumentException">Thrown when the currency code is not supported.</exception>
    public void SetPreferredCurrency(string preferredCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredCurrency);

        var normalized = preferredCurrency.ToUpperInvariant();
        if (normalized is not ("USD" or "EUR" or "EGP" or "SAR"))
        {
            throw new ArgumentException("Preferred currency must be USD, EUR, EGP, or SAR.", nameof(preferredCurrency));
        }

        PreferredCurrency = normalized;
    }

    /// <summary>
    /// Sets the URL of the user's avatar image.
    /// </summary>
    /// <param name="avatarUrl">The avatar URL, or <see langword="null"/> to clear it.</param>
    /// <exception cref="ArgumentException">Thrown when the avatar URL exceeds the maximum length.</exception>
    public void SetAvatarUrl(string? avatarUrl)
    {
        if (avatarUrl is not null && avatarUrl.Length > 500)
        {
            throw new ArgumentException("Avatar URL must not exceed 500 characters.", nameof(avatarUrl));
        }

        AvatarUrl = avatarUrl;
    }

    /// <summary>
    /// Updates the user's password hash and rotates the security stamp.
    /// </summary>
    /// <param name="passwordHash">The new hashed password.</param>
    /// <exception cref="ArgumentException">Thrown when the password hash is null or whitespace.</exception>
    public void UpdatePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Records a failed access attempt and applies lockout when the threshold is reached.
    /// </summary>
    /// <param name="maxFailedAttempts">The maximum allowed consecutive failures before lockout.</param>
    /// <param name="lockoutDuration">The duration of the lockout period.</param>
    public void RecordAccessFailure(int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;

        if (AccessFailedCount >= maxFailedAttempts)
        {
            LockoutEnd = DateTimeOffset.UtcNow.Add(lockoutDuration);
        }
    }

    /// <summary>
    /// Resets the access failed count to zero and clears any lockout.
    /// </summary>
    public void ResetAccessFailedCount()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }
}
