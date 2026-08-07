using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Application.Referrals;

/// <summary>
/// The contract Identity depends on to redeem a referral code at registration, without taking a hard
/// dependency on Commerce (which owns the referral domain). Implemented in Commerce.Application,
/// registered in DI — mirrors how <c>ICommunicationService</c> bridges Commerce to Communication.
/// </summary>
public interface IReferralRedemptionService
{
    /// <summary>Whether a referral code resolves to an existing referral profile.</summary>
    Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that <paramref name="referredUserId"/> signed up using <paramref name="referralCode"/>,
    /// creating a Pending referral history entry. Safe to call even if the referral program is disabled
    /// or the code no longer resolves — both are treated as a no-op rather than a failure, since the
    /// registration that triggered this call has already succeeded.
    /// </summary>
    Task<Result> RedeemAsync(string referralCode, string referredUserId, string referredEmail, CancellationToken cancellationToken = default);
}
