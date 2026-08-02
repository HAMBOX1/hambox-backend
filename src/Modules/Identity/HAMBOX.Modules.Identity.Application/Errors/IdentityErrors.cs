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

    public static readonly Error RegistrationNotAllowed = new(
        "Identity.RegistrationNotAllowed",
        "This email address cannot be used to register an account.");

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

    public static readonly Error RoleNotFound = new(
        "Identity.RoleNotFound",
        "The role was not found.");

    public static readonly Error RoleNameExists = new(
        "Identity.RoleNameExists",
        "A role with this name already exists.");

    public static readonly Error CannotDeleteSystemRole = new(
        "Identity.CannotDeleteSystemRole",
        "System roles cannot be deleted.");

    public static readonly Error CannotModifyOwnerRole = new(
        "Identity.CannotModifyOwnerRole",
        "The Owner role cannot be modified.");

    public static readonly Error InsufficientPrivileges = new(
        "Identity.InsufficientPrivileges",
        "You do not have sufficient privileges to manage this role.");

    public static readonly Error PermissionDenied = new(
        "Identity.PermissionDenied",
        "You do not have permission to perform this action.");

    public static readonly Error AdminMustUseAdminPortal = new(
        "Identity.AdminMustUseAdminPortal",
        "Administrative accounts must sign in through the admin portal.");

    public static readonly Error CustomerMustUseStorefrontLogin = new(
        "Identity.CustomerMustUseStorefrontLogin",
        "Customer accounts must sign in through the storefront login.");

    public static readonly Error AdminPortalAccessDenied = new(
        "Identity.AdminPortalAccessDenied",
        "You do not have permission to access the admin portal.");

    public static readonly Error AdminOtpInvalid = new(
        "Identity.AdminOtpInvalid",
        "The verification code is incorrect.");

    public static readonly Error AdminOtpExpired = new(
        "Identity.AdminOtpExpired",
        "The verification code has expired.");

    public static readonly Error AdminOtpLocked = new(
        "Identity.AdminOtpLocked",
        "Too many failed verification attempts. Try again later.");

    public static readonly Error AdminOtpResendCooldown = new(
        "Identity.AdminOtpResendCooldown",
        "Please wait before requesting another verification code.");

    public static readonly Error AdminLoginChallengeNotFound = new(
        "Identity.AdminLoginChallengeNotFound",
        "The login challenge is invalid or has expired.");

    public static readonly Error CustomerContextRequired = new(
        "Identity.CustomerContextRequired",
        "This action requires a customer session.");

    public static readonly Error AdminContextRequired = new(
        "Identity.AdminContextRequired",
        "This action requires an authenticated admin session with OTP verification.");

    public static readonly Error GoogleTokenInvalid = new(
        "Identity.GoogleTokenInvalid",
        "The Google sign-in token is invalid or could not be verified.");

    public static readonly Error GoogleAuthNotConfigured = new(
        "Identity.GoogleAuthNotConfigured",
        "Google sign-in is not configured on this server.");

    public static readonly Error CannotRestrictOwner = new(
        "Identity.CannotRestrictOwner",
        "An Owner account cannot be suspended, blocked, or banned.");

    public static readonly Error UserNotRestricted = new(
        "Identity.UserNotRestricted",
        "The user account is not currently suspended, blocked, or banned.");

    public static readonly Error InvalidUserStateTransition = new(
        "Identity.InvalidUserStateTransition",
        "The requested action is not valid for the user's current status.");

    public static readonly Error CountryRestrictionNotFound = new(
        "Identity.CountryRestrictionNotFound",
        "The country restriction was not found.");

    public static readonly Error BlockedEmailNotFound = new(
        "Identity.BlockedEmailNotFound",
        "The blocked email entry was not found.");

    public static readonly Error BlockedIpNotFound = new(
        "Identity.BlockedIpNotFound",
        "The blocked IP entry was not found.");

    public static readonly Error BlockedEmailAlreadyExists = new(
        "Identity.BlockedEmailAlreadyExists",
        "This email address or domain pattern is already blocked.");

    public static readonly Error BlockedIpAlreadyExists = new(
        "Identity.BlockedIpAlreadyExists",
        "This IP address or range is already blocked.");

    public static readonly Error SessionNotFound = new(
        "Identity.SessionNotFound",
        "The session was not found.");

    public static readonly Error TrustedDeviceNotFound = new(
        "Identity.TrustedDeviceNotFound",
        "The device was not found.");

    public static readonly Error SecurityEventNotFound = new(
        "Identity.SecurityEventNotFound",
        "The security event was not found.");
}
