using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Identity.Application.Errors;

/// <summary>
/// Contains well-known identity domain errors for the Result pattern.
/// </summary>
public static class IdentityErrors
{
    public static readonly Error EmailAlreadyExists = new(
        "Identity.EmailAlreadyExists",
        "A user with this email address already exists.");

    public static readonly Error InvalidCredentials = new(
        "Identity.InvalidCredentials",
        "The email address or password is incorrect.");

    public static readonly Error EmailNotConfirmed = new(
        "Identity.EmailNotConfirmed",
        "The email address has not been confirmed.");

    public static readonly Error AccountNotActive = new(
        "Identity.AccountNotActive",
        "The user account is not active.");

    public static readonly Error InvalidToken = new(
        "Identity.InvalidToken",
        "The token is invalid or has already been used.");

    public static readonly Error TokenExpired = new(
        "Identity.TokenExpired",
        "The token has expired.");

    public static readonly Error AccountLocked = new(
        "Identity.AccountLocked",
        "The user account is locked due to too many failed access attempts.");

    public static readonly Error UserNotFound = new(
        "Identity.UserNotFound",
        "The user was not found.");

    public static readonly Error DefaultRoleNotFound = new(
        "Identity.DefaultRoleNotFound",
        "The default customer role is not configured.");

    public static readonly Error AuthenticationRequired = new(
        "Identity.AuthenticationRequired",
        "Authentication is required to perform this action.");

    public static readonly Error InvalidCurrentPassword = new(
        "Identity.InvalidCurrentPassword",
        "The current password is incorrect.");

    public static readonly Error ProfileUpdateFailed = new(
        "Identity.ProfileUpdateFailed",
        "The profile could not be updated.");
}
