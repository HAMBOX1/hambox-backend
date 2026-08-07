using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Referrals;

/// <summary>
/// Outcome of <see cref="ReferralLifecycleService.ProcessOrderCompletedAsync"/>, distinguishing "there
/// was nothing to do" from "there was a referral, but it's no longer claimable".
/// </summary>
public enum ReferralQualificationOutcome
{
    /// <summary>The buyer has no Pending referral — nothing to claim, nothing was ever offered.</summary>
    NotApplicable,

    /// <summary>This order successfully claimed the buyer's Pending referral.</summary>
    Qualified,

    /// <summary>
    /// The buyer had a Pending referral, but it's no longer claimable by this order — either a
    /// concurrent order already won the race, or it expired.
    /// </summary>
    Ineligible,
}

/// <summary>
/// Owns the referral lifecycle from redemption at registration through reward issuance on the referred
/// user's first completed order, and reversal if that order is later refunded or cancelled. Implements
/// the BuildingBlocks <see cref="IReferralRedemptionService"/> contract (consumed by Identity) and
/// additionally exposes <see cref="ProcessOrderCompletedAsync"/>/<see cref="ReverseForOrderAsync"/> for
/// the Commerce checkout/fulfillment/refund paths that already live in this module. Public (not
/// internal) because Commerce.Infrastructure's background job handlers call it too.
/// </summary>
public sealed class ReferralLifecycleService : IReferralRedemptionService
{
    private readonly ICommerceDbContext _db;
    private readonly IPlatformSettingsProvider _platformSettings;
    private readonly ReferralRewardService _rewardService;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<ReferralLifecycleService> _logger;

    public ReferralLifecycleService(
        ICommerceDbContext db,
        IPlatformSettingsProvider platformSettings,
        ReferralRewardService rewardService,
        ICommunicationService communicationService,
        ILogger<ReferralLifecycleService> logger)
    {
        _db = db;
        _platformSettings = platformSettings;
        _rewardService = rewardService;
        _communicationService = communicationService;
        _logger = logger;
    }

    public async Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken cancellationToken = default) =>
        await _db.ReferralProfiles.AnyAsync(p => p.ReferralCode == referralCode, cancellationToken);

    public async Task<Result> RedeemAsync(string referralCode, string referredUserId, string referredEmail, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        if (!settings.Enabled)
        {
            return Result.Success();
        }

        var referrer = await _db.ReferralProfiles
            .FirstOrDefaultAsync(p => p.ReferralCode == referralCode, cancellationToken);

        // referrer.UserId == referredUserId (self-referral) is structurally unreachable here — the
        // referred user is a brand-new registration that can't already own the code being redeemed —
        // but the check (and the identical guard in ReferralHistoryEntry.CreatePending) stays as
        // defense-in-depth in case this is ever called from a second entry point.
        if (referrer is null || referrer.UserId == referredUserId)
        {
            return Result.Success();
        }

        // A brand-new registrant can't already have a referral entry; this guards a second entry
        // point ever calling RedeemAsync post-registration, and backs the DB unique index on
        // ReferredUserId that would otherwise surface as an unhandled constraint violation.
        var alreadyReferred = await _db.ReferralHistoryEntries
            .AnyAsync(h => h.ReferredUserId == referredUserId, cancellationToken);

        if (alreadyReferred)
        {
            return Result.Success();
        }

        var expiresOnUtc = DateTime.UtcNow.AddDays(settings.RewardExpiryDays);
        var entry = ReferralHistoryEntry.CreatePending(referrer.UserId, referredUserId, referredEmail, expiresOnUtc);
        _db.ReferralHistoryEntries.Add(entry);
        _db.ReferralAuditLogs.Add(ReferralAuditLog.Create(
            entry.Id,
            referrer.UserId,
            ReferralAuditAction.Redeemed,
            points: null,
            performedByUserId: referredUserId,
            details: $"Referral code {referralCode} redeemed at registration."));

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Referral {ReferralId} redeemed: referrer {ReferrerUserId}, referred {ReferredUserId}.",
            entry.Id, referrer.UserId, referredUserId);

        await _communicationService.SendAsync(new CommunicationRequest(
            UserId: referrer.UserId,
            TemplateKey: "ReferralFriendJoined",
            Category: CommunicationCategory.Promotion,
            Variables: new Dictionary<string, string> { ["ReferralCode"] = referralCode },
            RelatedEntityType: "ReferralHistoryEntry",
            RelatedEntityId: entry.Id.ToString(),
            ActionUrl: "/account/referral"), cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Called from every code path that can transition an order to Completed. Qualifies and rewards
    /// the referrer if the referred user has a still-Pending referral and this is the order that
    /// resolves it — idempotent and concurrency-safe: the Pending -&gt; Qualified transition is guarded
    /// by an optimistic concurrency token, so if two of the referred user's orders complete at the same
    /// instant, only one can win it, and calling this twice for the same order (a retried job, a
    /// duplicate webhook) is a no-op the second time because the entry is no longer Pending.
    /// </summary>
    public async Task<ReferralQualificationOutcome> ProcessOrderCompletedAsync(Order order, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            return ReferralQualificationOutcome.NotApplicable;
        }

        var entry = await _db.ReferralHistoryEntries
            .FirstOrDefaultAsync(
                h => h.ReferredUserId == order.UserId && h.Status == ReferralStatus.Pending,
                cancellationToken);

        if (entry is null)
        {
            return ReferralQualificationOutcome.NotApplicable;
        }

        var isExpired = entry.ExpiresOnUtc is DateTime expiresOnUtc && DateTime.UtcNow > expiresOnUtc;

        if (isExpired)
        {
            await TryTransitionAsync(
                entry,
                entry.MarkExpired,
                ReferralAuditAction.Expired,
                points: null,
                performedByUserId: "system",
                details: "Referral expired before the referred user's first completed order.",
                cancellationToken);

            return ReferralQualificationOutcome.Ineligible;
        }

        var qualified = await TryTransitionAsync(
            entry,
            entry.MarkQualified,
            ReferralAuditAction.Qualified,
            points: null,
            performedByUserId: "system",
            details: $"Qualified by order {order.OrderNumber}.",
            cancellationToken);

        if (!qualified)
        {
            // Lost the race to a concurrent completion — the winner already claimed this entry.
            return ReferralQualificationOutcome.Ineligible;
        }

        var basePoints = settings.PointsPerReferral;

        if (basePoints <= 0)
        {
            _logger.LogInformation(
                "Referral {ReferralId} qualified by order {OrderNumber} with a zero computed reward; no points awarded.",
                entry.Id, order.OrderNumber);
            return ReferralQualificationOutcome.Qualified;
        }

        var awardedPoints = await _rewardService.AwardPointsAsync(entry.ReferrerUserId, basePoints, cancellationToken);
        if (awardedPoints <= 0)
        {
            return ReferralQualificationOutcome.Qualified;
        }

        var rewarded = await TryTransitionAsync(
            entry,
            () => entry.AwardPoints(awardedPoints),
            ReferralAuditAction.Rewarded,
            awardedPoints,
            performedByUserId: "system",
            details: $"Rewarded for order {order.OrderNumber}.",
            cancellationToken);

        if (rewarded)
        {
            await _communicationService.SendAsync(new CommunicationRequest(
                UserId: entry.ReferrerUserId,
                TemplateKey: "ReferralRewardEarned",
                Category: CommunicationCategory.Promotion,
                Variables: new Dictionary<string, string> { ["Points"] = awardedPoints.ToString() },
                RelatedEntityType: "ReferralHistoryEntry",
                RelatedEntityId: entry.Id.ToString(),
                ActionUrl: "/account/referral"), cancellationToken);
        }

        return ReferralQualificationOutcome.Qualified;
    }

    /// <summary>
    /// Called when an order is cancelled or refunded. A still-Pending referral for that order's buyer
    /// expires (it will never qualify through this order); a Qualified or Rewarded referral is
    /// reversed, clawing back any points already awarded. Idempotent — calling this again after a
    /// second refund attempt on an already-reversed entry is a no-op.
    /// </summary>
    public async Task ReverseForOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var entry = await _db.ReferralHistoryEntries
            .FirstOrDefaultAsync(h => h.ReferredUserId == order.UserId, cancellationToken);

        if (entry is null)
        {
            return;
        }

        switch (entry.Status)
        {
            case ReferralStatus.Pending:
                await TryTransitionAsync(
                    entry,
                    entry.MarkExpired,
                    ReferralAuditAction.Expired,
                    points: null,
                    performedByUserId: "system",
                    details: $"Order {order.OrderNumber} was cancelled or refunded before the referral could qualify.",
                    cancellationToken);
                break;

            case ReferralStatus.Qualified or ReferralStatus.Rewarded:
                var pointsToClawBack = entry.PointsEarned;

                var reversed = await TryTransitionAsync(
                    entry,
                    entry.Reverse,
                    ReferralAuditAction.Reversed,
                    points: pointsToClawBack > 0 ? -pointsToClawBack : null,
                    performedByUserId: "system",
                    details: $"Reversed because order {order.OrderNumber} was cancelled or refunded.",
                    cancellationToken);

                if (reversed && pointsToClawBack > 0)
                {
                    await _rewardService.ReverseAsync(entry.ReferrerUserId, pointsToClawBack, cancellationToken);
                }

                break;

            default:
                // Already Expired/Reversed — idempotent no-op.
                break;
        }
    }

    private async Task<ReferralSettingsPayload> GetSettingsAsync(CancellationToken cancellationToken) =>
        await _platformSettings.GetAsync<ReferralSettingsPayload>(PlatformSettingsCategoryKeys.Referral, cancellationToken);

    /// <summary>
    /// Applies one state-machine transition to a tracked <see cref="ReferralHistoryEntry"/>, stages its
    /// audit row, and saves both atomically. Returns false — without throwing — if a concurrent writer
    /// already changed this entry's status first (detected via its optimistic concurrency token): that
    /// means the desired outcome (this transition happens at most once) was already achieved by the
    /// winner, so losing the race here is a correct no-op, not a failure.
    /// </summary>
    private async Task<bool> TryTransitionAsync(
        ReferralHistoryEntry entry,
        Action transition,
        ReferralAuditAction auditAction,
        int? points,
        string performedByUserId,
        string details,
        CancellationToken cancellationToken)
    {
        transition();
        _db.ReferralAuditLogs.Add(ReferralAuditLog.Create(entry.Id, entry.ReferrerUserId, auditAction, points, performedByUserId, details));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        _logger.LogInformation(
            "Referral {ReferralId} {Action} for referrer {ReferrerUserId}: {Details}",
            entry.Id, auditAction, entry.ReferrerUserId, details);

        return true;
    }
}
