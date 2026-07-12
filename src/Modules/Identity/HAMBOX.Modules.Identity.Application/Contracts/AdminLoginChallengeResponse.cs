namespace HAMBOX.Modules.Identity.Application.Contracts;

/// <summary>
/// Response returned after admin credential validation, before OTP verification.
/// </summary>
/// <param name="ChallengeId">The challenge identifier for OTP verification.</param>
/// <param name="ExpiresAt">When the OTP expires.</param>
/// <param name="ResendAvailableAt">Earliest time a resend is allowed.</param>
/// <param name="MaskedEmail">Masked destination email.</param>
/// <param name="Token">
/// Populated only when Admin OTP is disabled via Platform Settings (Authentication.AdminOtpEnabled = false),
/// in which case the caller is already fully authenticated and should skip the OTP step.
/// </param>
public sealed record AdminLoginChallengeResponse(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResendAvailableAt,
    string MaskedEmail,
    AuthTokenResponse? Token = null);
