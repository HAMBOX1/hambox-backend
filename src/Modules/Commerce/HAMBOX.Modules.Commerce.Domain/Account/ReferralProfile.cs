using System.Security.Cryptography;
using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Represents a user's referral program profile.
/// </summary>
public sealed class ReferralProfile : AggregateRoot
{
    private const string ReferralCodePrefix = "HAM-";
    private const string ReferralCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private ReferralProfile()
    {
    }

    private ReferralProfile(Guid id, string userId, string referralCode, string tier)
        : base(id)
    {
        UserId = userId;
        ReferralCode = referralCode;
        Tier = tier;
        LifetimePoints = 0;
        SuccessfulReferrals = 0;
    }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the unique referral code.
    /// </summary>
    public string ReferralCode { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the referral tier.
    /// </summary>
    public string Tier { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the lifetime referral points earned.
    /// </summary>
    public int LifetimePoints { get; private set; }

    /// <summary>
    /// Gets the number of successful referrals.
    /// </summary>
    public int SuccessfulReferrals { get; private set; }

    /// <summary>
    /// Creates a referral profile for a user.
    /// </summary>
    public static ReferralProfile CreateForUser(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return new ReferralProfile(
            Guid.NewGuid(),
            userId,
            GenerateReferralCode(),
            "Starter");
    }

    /// <summary>
    /// Awards referral points for a successful referral.
    /// </summary>
    public void AwardReferralPoints(int points)
    {
        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Points must be greater than zero.");
        }

        LifetimePoints += points;
        SuccessfulReferrals++;
        Tier = ResolveTier(LifetimePoints);
    }

    private static string GenerateReferralCode()
    {
        Span<char> code = stackalloc char[8];

        for (var i = 0; i < code.Length; i++)
        {
            code[i] = ReferralCodeChars[RandomNumberGenerator.GetInt32(ReferralCodeChars.Length)];
        }

        return ReferralCodePrefix + new string(code);
    }

    private static string ResolveTier(int lifetimePoints) => lifetimePoints switch
    {
        >= 1000 => "Gold",
        >= 500 => "Silver",
        >= 100 => "Bronze",
        _ => "Starter"
    };
}
