using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// A redeemable code that grants an ELITE period — a gift pass, a trial, a promotional offer or an admin grant. Every
/// redemption is recorded (who, when, which subscription); a customer can use a given code once; and a granted period
/// never turns into an automatic renewal.
/// </summary>
public sealed class MembershipCode : AggregateRoot
{
    private MembershipCode()
    {
    }

    private MembershipCode(Guid id)
        : base(id)
    {
    }

    public string Code { get; private set; } = string.Empty;

    public MembershipCodeKind Kind { get; private set; }

    public Guid PlanId { get; private set; }

    /// <summary>For trials: grant this many days instead of the plan's own duration. Null = the plan's duration.</summary>
    public int? GrantDays { get; private set; }

    /// <summary>Total redemptions allowed across all customers.</summary>
    public int MaxRedemptions { get; private set; }

    public int RedeemedCount { get; private set; }

    public DateTimeOffset? ExpiresOnUtc { get; private set; }

    public bool IsActive { get; private set; } = true;

    public string? Note { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public static MembershipCode Create(
        string code, MembershipCodeKind kind, Guid planId, int? grantDays, int maxRedemptions,
        DateTimeOffset? expiresOnUtc, string? note, string? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRedemptions, 1);
        if (grantDays is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grantDays));
        }

        return new MembershipCode(Guid.NewGuid())
        {
            Code = code.Trim().ToUpperInvariant(),
            Kind = kind,
            PlanId = planId,
            GrantDays = grantDays,
            MaxRedemptions = maxRedemptions,
            ExpiresOnUtc = expiresOnUtc,
            Note = note?.Trim(),
            CreatedByUserId = createdByUserId,
        };
    }

    public bool CanRedeemAt(DateTimeOffset now) =>
        IsActive && RedeemedCount < MaxRedemptions && (ExpiresOnUtc is null || ExpiresOnUtc > now);

    public void MarkRedeemed() => RedeemedCount++;

    public void SetActive(bool isActive) => IsActive = isActive;
}

/// <summary>Who redeemed which code, and the subscription it produced. Unique per (code, user).</summary>
public sealed class MembershipCodeRedemption : Entity
{
    private MembershipCodeRedemption()
    {
    }

    private MembershipCodeRedemption(Guid id)
        : base(id)
    {
    }

    public Guid CodeId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public Guid SubscriptionId { get; private set; }

    public DateTimeOffset RedeemedOnUtc { get; private set; }

    public static MembershipCodeRedemption Create(Guid codeId, string userId, Guid subscriptionId) =>
        new(Guid.NewGuid())
        {
            CodeId = codeId,
            UserId = userId,
            SubscriptionId = subscriptionId,
            RedeemedOnUtc = DateTimeOffset.UtcNow,
        };
}
