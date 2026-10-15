using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.Dot.TestDoubles;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Referrals;

/// <summary>Audit R49: referral farming (shared address, sign-up bursts) is held for an admin, never rewarded silently.</summary>
public sealed class ReferralAntiAbuseTests
{
    private static ReferralLifecycleService Service(CommerceDbContext db) =>
        new(db, new FakeDotPlatformSettingsProvider { ReferralEnabled = true },
            new ReferralRewardService(db, new FakeMembershipEngine()),
            new FakeCommunicationService(), NullLogger<ReferralLifecycleService>.Instance);

    private static async Task<string> ReferrerAsync(CommerceDbContext db)
    {
        var profile = ReferralProfile.CreateForUser("referrer-1");
        db.ReferralProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.ReferralCode;
    }

    private static Order CompletedOrder(string userId)
    {
        var order = Order.Create(
            userId, $"ORD-{Guid.NewGuid():N}", $"{userId}@example.com", "US", "dot", 20m, 0m, 0m, 20m,
            [(Guid.NewGuid(), "Gift card", 1, 20m, (Guid?)null, (string?)null, (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)]);
        order.RecordPayment("Dot", $"txn-{Guid.NewGuid():N}");
        order.MarkProcessing();
        order.Complete();
        return order;
    }

    [Fact]
    public async Task ASingleSignUp_IsNotHeld()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var code = await ReferrerAsync(db);

        await Service(db).RedeemAsync(code, "friend-1", "f1@example.com", "198.51.100.7");

        var entry = await db.ReferralHistoryEntries.SingleAsync();
        Assert.False(entry.IsHeldForReview);
        Assert.Equal("198.51.100.7", entry.RegistrationIp);
    }

    [Fact]
    public async Task SecondSignUpFromTheSameAddress_IsHeld_AndAudited()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var code = await ReferrerAsync(db);
        var service = Service(db);

        await service.RedeemAsync(code, "friend-1", "f1@example.com", "198.51.100.7");
        await service.RedeemAsync(code, "friend-2", "f2@example.com", "198.51.100.7");

        var second = await db.ReferralHistoryEntries.SingleAsync(h => h.ReferredUserId == "friend-2");
        Assert.True(second.IsHeldForReview);
        Assert.Contains("same address", second.ReviewReason);
        Assert.Contains(db.ReferralAuditLogs, l => l.Action == ReferralAuditAction.HeldForReview);
    }

    [Fact]
    public async Task ABurstOfSignUps_FromDifferentAddresses_IsHeldPastTheDailyLimit()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var code = await ReferrerAsync(db);
        var service = Service(db);

        for (var i = 1; i <= ReferralLifecycleService.ReferralsPerDayBeforeReview + 1; i++)
        {
            await service.RedeemAsync(code, $"friend-{i}", $"f{i}@example.com", $"203.0.113.{i}");

            // The audit interceptor is not part of the test context, so stamp the creation time by hand.
            var created = await db.ReferralHistoryEntries.SingleAsync(h => h.ReferredUserId == $"friend-{i}");
            db.Entry(created).Property(h => h.CreatedOnUtc).CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var held = await db.ReferralHistoryEntries.CountAsync(h => h.ReviewReason != null);
        Assert.Equal(1, held);
    }

    [Fact]
    public async Task AHeldReferral_IsNotQualifiedByAnOrder_UntilAnAdminApprovesIt()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var code = await ReferrerAsync(db);
        var service = Service(db);
        await service.RedeemAsync(code, "friend-1", "f1@example.com", "198.51.100.7");
        await service.RedeemAsync(code, "friend-2", "f2@example.com", "198.51.100.7");

        var order = CompletedOrder("friend-2");
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var outcome = await service.ProcessOrderCompletedAsync(order, CancellationToken.None);

        var entry = await db.ReferralHistoryEntries.SingleAsync(h => h.ReferredUserId == "friend-2");
        Assert.Equal(ReferralQualificationOutcome.Ineligible, outcome);
        Assert.Equal(ReferralStatus.Pending, entry.Status);

        var approved = await service.AdminApproveReviewAsync(entry.Id, "admin-1", CancellationToken.None);

        Assert.True(approved.IsSuccess);
        await db.Entry(entry).ReloadAsync();
        Assert.False(entry.IsHeldForReview);
        Assert.Equal("admin-1", entry.ReviewApprovedByUserId);
        Assert.NotEqual(ReferralStatus.Pending, entry.Status);
        Assert.Contains(db.ReferralAuditLogs, l => l.Action == ReferralAuditAction.ReviewApproved);
    }

    [Fact]
    public async Task ApprovingAReferralThatIsNotHeld_IsRefused()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var code = await ReferrerAsync(db);
        var service = Service(db);
        await service.RedeemAsync(code, "friend-1", "f1@example.com", "198.51.100.7");
        var entry = await db.ReferralHistoryEntries.SingleAsync();

        var result = await service.AdminApproveReviewAsync(entry.Id, "admin-1", CancellationToken.None);

        Assert.Equal(CommerceErrors.ReferralNotHeldForReview.Code, result.Error.Code);
    }
}
