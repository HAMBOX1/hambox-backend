namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Represents the authenticated user's profile information.
/// </summary>
/// <param name="Id">The user's unique identifier.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
/// <param name="PhoneNumber">The user's phone number.</param>
/// <param name="AvatarUrl">The URL of the user's avatar image.</param>
/// <param name="EmailConfirmed">Whether the user's email address has been confirmed.</param>
/// <param name="Status">The current account status.</param>
/// <param name="MemberSince">The date and time, in UTC, when the account was created.</param>
public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? AvatarUrl,
    bool EmailConfirmed,
    string Status,
    string PreferredLanguage,
    string PreferredCurrency,
    DateTimeOffset MemberSince,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
