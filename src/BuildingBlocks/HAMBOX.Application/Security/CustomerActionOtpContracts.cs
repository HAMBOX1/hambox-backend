namespace HAMBOX.Application.Security;

public enum CustomerActionOtpIssueStatus
{
    /// <summary>A fresh code was e-mailed to the customer.</summary>
    Sent = 0,

    /// <summary>A code was requested too recently; <see cref="CustomerActionOtpIssueResult.ResendAvailableAt"/> says when.</summary>
    CooldownActive = 1,

    /// <summary>The code could not be delivered; the challenge was discarded.</summary>
    DeliveryFailed = 2,

    /// <summary>The customer is temporarily locked out after too many wrong codes.</summary>
    Locked = 3,

    UserNotFound = 4,
}

public sealed record CustomerActionOtpIssueResult(
    CustomerActionOtpIssueStatus Status,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? ResendAvailableAt = null,
    string? MaskedEmail = null);

public enum CustomerActionOtpVerifyStatus
{
    Verified = 0,
    Invalid = 1,
    Expired = 2,
    Locked = 3,

    /// <summary>No code was ever requested for this action (or it was already used).</summary>
    NotRequested = 4,
}

/// <summary>
/// A one-time e-mailed code that must be entered before a sensitive customer action — today, showing a
/// purchased license key in plaintext. Codes are hashed at rest, expire, are single-use and lock after too many
/// wrong attempts. Implemented by the Identity module; Commerce depends only on this contract.
/// </summary>
public interface ICustomerActionOtpService
{
    /// <param name="purpose">Stable action name, e.g. <c>LicenseKeyReveal</c>.</param>
    /// <param name="scopeKey">What the code is bound to (the license key id) — a code for one key never opens another.</param>
    Task<CustomerActionOtpIssueResult> IssueAsync(
        string userId, string purpose, string scopeKey, CancellationToken cancellationToken = default);

    /// <summary>Checks and, when correct, spends the customer's latest code for this action and scope.</summary>
    Task<CustomerActionOtpVerifyStatus> VerifyAsync(
        string userId, string purpose, string scopeKey, string code, CancellationToken cancellationToken = default);
}
