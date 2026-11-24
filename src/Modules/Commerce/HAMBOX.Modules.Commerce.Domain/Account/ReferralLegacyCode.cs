using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// A referral code issued by the retired PRIME programme, kept working under its original owner. It resolves to the same
/// <see cref="ReferralProfile"/> as the owner's current code, so an old code follows the unified Referral rules and
/// never pays a second, separate reward.
/// </summary>
public sealed class ReferralLegacyCode : Entity
{
    private ReferralLegacyCode()
    {
    }

    private ReferralLegacyCode(Guid id)
        : base(id)
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string OwnerUserId { get; private set; } = string.Empty;

    public string Source { get; private set; } = string.Empty;

    public DateTimeOffset ImportedOnUtc { get; private set; }

    public static ReferralLegacyCode Create(string code, string ownerUserId, string source) =>
        new(Guid.NewGuid())
        {
            Code = code.Trim().ToUpperInvariant(),
            OwnerUserId = ownerUserId,
            Source = source,
            ImportedOnUtc = DateTimeOffset.UtcNow,
        };
}
