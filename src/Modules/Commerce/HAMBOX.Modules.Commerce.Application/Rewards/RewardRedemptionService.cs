using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Membership;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Loyalty;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Rewards;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Rewards;

public static class RewardErrors
{
    public static readonly Error Disabled = new("Rewards.Disabled", "The Stars Shop is not available right now.");
    public static readonly Error NotFound = new("Rewards.NotFound", "This reward is not available.");
    public static readonly Error NotEligible = new("Rewards.NotEligible", "Your account is not eligible for this reward.");
    public static readonly Error InsufficientStars = new("Rewards.InsufficientStars", "You don't have enough available Stars for this reward.");
    public static readonly Error OutOfStock = new("Rewards.OutOfStock", "This reward is out of stock.");
    public static readonly Error LimitReached = new("Rewards.LimitReached", "You have reached the limit for this reward.");
    public static readonly Error GiftAlreadyClaimed = new("Rewards.GiftAlreadyClaimed", "You have already claimed this period's gift.");
    public static readonly Error GiftNotAvailable = new("Rewards.GiftNotAvailable", "The monthly gift is not available for your membership.");
}

public sealed record RewardRedemptionResult(Guid OrderId, string OrderNumber, int StarsSpent, string Status, string? CouponCode = null);

/// <summary>
/// Redeems a reward for Stars. It produces an ordinary order (zero cash, payment method "stars") so delivery, support and
/// refunds work exactly like any other purchase: stock is reserved with the Stars in one transaction, the code is
/// delivered, and only then do the Stars become final. A failure rolls back both, so nothing is spent or delivered
/// twice. The ELITE monthly gift reuses the same path with a zero Stars price.
/// </summary>
public sealed class RewardRedemptionService(
    ICommerceDbContext db,
    ICatalogDbContext catalog,
    ICommerceTransactionService transactions,
    IInventoryEngine inventory,
    IMembershipAccessProvider membership,
    LoyaltyRankService ranks,
    StarsLedgerService stars,
    ReferralLifecycleService lifecycle,
    IOperationalJobQueue jobQueue,
    IPlatformSettingsProvider settings,
    ILogger<RewardRedemptionService> logger,
    ISender sender)
{
    public async Task<bool> IsShopEnabledAsync(CancellationToken cancellationToken) =>
        (await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken)).StarsShop;

    /// <summary>Why <paramref name="reward"/> can't be redeemed by this customer, or null when they qualify.</summary>
    public async Task<string?> LockReasonAsync(RewardItem reward, string userId, CancellationToken cancellationToken)
    {
        if (reward.RequiresElite && !(await membership.GetAccessInfoAsync(userId, cancellationToken)).SubscriptionId.HasValue)
        {
            return "Elite";
        }

        if (reward.MinRankKey is not null)
        {
            var all = await ranks.EnsureRanksAsync(cancellationToken);
            var needed = all.FirstOrDefault(r => r.Key == reward.MinRankKey)?.SortOrder ?? 0;
            var mine = (await ranks.GetStatusAsync(userId, cancellationToken)).Current.SortOrder;
            if (mine < needed)
            {
                return "Rank";
            }
        }

        return null;
    }

    public async Task<Result<RewardRedemptionResult>> RedeemAsync(
        string userId,
        string email,
        string country,
        Guid rewardItemId,
        RewardRedemptionSource source,
        string? periodKey,
        CancellationToken cancellationToken)
    {
        if (source == RewardRedemptionSource.StarsShop && !await IsShopEnabledAsync(cancellationToken))
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.Disabled);
        }

        var reward = await db.RewardItems.FirstOrDefaultAsync(r => r.Id == rewardItemId, cancellationToken);
        if (reward is null || !reward.IsLiveAt(DateTimeOffset.UtcNow))
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.NotFound);
        }

        if (source == RewardRedemptionSource.StarsShop && await LockReasonAsync(reward, userId, cancellationToken) is not null)
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.NotEligible);
        }

        if (reward.MaxPerUser is { } perUser
            && await db.RewardRedemptions.CountAsync(
                r => r.UserId == userId && r.RewardItemId == reward.Id && r.Status != RewardRedemptionStatus.Failed, cancellationToken) >= perUser)
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.LimitReached);
        }

        if (reward.IsCoupon)
        {
            return await RedeemCouponAsync(reward, userId, reward.PriceAt(DateTimeOffset.UtcNow), cancellationToken);
        }

        var product = await catalog.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == reward.ProductId, cancellationToken);
        var variant = await catalog.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == reward.ProductVariantId, cancellationToken);
        if (product is null || variant is null || product.Status != ProductStatus.Active || variant.Status != ProductVariantStatus.Active)
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.NotFound);
        }

        if (reward.MaxPerUser is { } cap
            && await db.RewardRedemptions.CountAsync(
                r => r.UserId == userId && r.RewardItemId == reward.Id && r.Status != RewardRedemptionStatus.Failed, cancellationToken) >= cap)
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.LimitReached);
        }

        var cost = source == RewardRedemptionSource.EliteMonthlyGift ? 0 : reward.PriceAt(DateTimeOffset.UtcNow);
        var commerce = await settings.GetAsync<CommerceSettingsPayload>(PlatformSettingsCategoryKeys.Commerce, cancellationToken);
        var mode = variant.FulfillmentMode;
        var manual = mode is FulfillmentMode.ManualOnly or FulfillmentMode.ManualFirst;
        var value = variant.PriceOverride ?? product.Price;

        Order? order = null;
        var needsJob = false;
        Error? failure = null;

        try
        {
            await transactions.ExecuteAsync(async ct =>
            {
                if (reward.RemainingQuantity.HasValue)
                {
                    var taken = await db.RewardItems
                        .Where(r => r.Id == reward.Id && r.RemainingQuantity > 0)
                        .ExecuteUpdateAsync(s => s.SetProperty(r => r.RemainingQuantity, r => r.RemainingQuantity - 1), ct);
                    if (taken == 0)
                    {
                        failure = RewardErrors.OutOfStock;
                        return;
                    }
                }

                var reserved = manual
                    ? await inventory.ReservePartialCodesAsync(variant.Id, 1, userId, Guid.NewGuid(), ct)
                    : [];
                if (mode == FulfillmentMode.ManualOnly && reserved.Count == 0)
                {
                    failure = RewardErrors.OutOfStock;
                    throw new RewardAbortException();
                }

                order = Order.Create(
                    userId, OrderNumberGenerator.Generate(commerce.InvoicePrefix), email, country, "stars",
                    value, value, 0m, 0m,
                    [(product.Id, product.NameEn, 1, value, (Guid?)variant.Id, variant.Sku, null, null, null, null)]);
                order.RecordPayment("Stars", Guid.NewGuid().ToString());

                if (cost > 0
                    && !await stars.ReserveForOrderAsync(userId, cost, order.Id, order.OrderNumber, $"Stars Shop: {product.NameEn}", ct))
                {
                    failure = RewardErrors.InsufficientStars;
                    throw new RewardAbortException();
                }

                var item = order.Items.First();
                var delivered = 0;
                if (reserved.Count > 0)
                {
                    var committed = await inventory.CommitReservationsAsync(order.Id, [(item.Id, reserved[0].CodeId)], ct);
                    foreach (var code in committed)
                    {
                        db.OrderLicenseKeys.Add(OrderLicenseKey.Create(
                            order.Id, item.Id, product.Id, code.DigitalCode, variant.Id, code.CodeId));
                        delivered++;
                    }
                }

                db.Orders.Add(order);
                db.RewardRedemptions.Add(RewardRedemption.Create(userId, reward.Id, order.Id, cost, source, periodKey));

                if (delivered >= 1)
                {
                    order.Complete();
                    await lifecycle.ProcessOrderCompletedAsync(order, ct);
                }
                else
                {
                    order.MarkProcessing();
                    needsJob = true;
                }

                await db.SaveChangesAsync(ct);
                await catalog.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (RewardAbortException) when (failure is not null)
        {
            return Result.Failure<RewardRedemptionResult>(failure);
        }
        catch (DbUpdateException ex) when (periodKey is not null)
        {
            logger.LogInformation(ex, "Monthly gift for {UserId} / {Period} was claimed concurrently.", userId, periodKey);
            return Result.Failure<RewardRedemptionResult>(RewardErrors.GiftAlreadyClaimed);
        }

        if (failure is not null || order is null)
        {
            return Result.Failure<RewardRedemptionResult>(failure ?? RewardErrors.NotFound);
        }

        if (needsJob)
        {
            await jobQueue.EnqueueAsync(
                OperationalJobTypes.ExecuteOrderFulfillment,
                priority: OperationalJobPriority.High,
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        return Result.Success(new RewardRedemptionResult(order.Id, order.OrderNumber, cost, order.Status.ToString()));
    }

    /// <summary>
    /// A coupon reward: Stars are spent and a fresh single-use code is generated from the reward's coupon campaign. If the code
    /// cannot be generated, the Stars are paid straight back and the reward's stock is restored.
    /// </summary>
    private async Task<Result<RewardRedemptionResult>> RedeemCouponAsync(RewardItem reward, string userId, int cost, CancellationToken cancellationToken)
    {
        if (reward.RemainingQuantity.HasValue)
        {
            var taken = await db.RewardItems
                .Where(r => r.Id == reward.Id && r.RemainingQuantity > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.RemainingQuantity, r => r.RemainingQuantity - 1), cancellationToken);
            if (taken == 0)
            {
                return Result.Failure<RewardRedemptionResult>(RewardErrors.OutOfStock);
            }
        }

        async Task RestoreStockAsync()
        {
            if (reward.RemainingQuantity.HasValue)
            {
                await db.RewardItems.Where(r => r.Id == reward.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.RemainingQuantity, r => r.RemainingQuantity + 1), cancellationToken);
            }
        }

        var redemption = RewardRedemption.Create(userId, reward.Id, Guid.Empty, cost, RewardRedemptionSource.StarsShop, null);
        var key = $"redeem-coupon:{redemption.Id:N}";
        if (!await stars.DebitAsync(userId, PointsTransactionType.Redeemed, cost, $"Stars Shop: {reward.TitleEn ?? "coupon"}", key, reward.Id.ToString(), cancellationToken))
        {
            await RestoreStockAsync();
            return Result.Failure<RewardRedemptionResult>(RewardErrors.InsufficientStars);
        }

        // DebitAsync does not check the balance (it also serves rightful clawbacks), so the balance is checked here, after the
        // debit, and a short balance is paid straight back: spendable Stars can never be driven below zero by a redemption.
        if ((await stars.GetBalanceAsync(userId, cancellationToken)).Available < 0)
        {
            await stars.CreditAsync(userId, PointsTransactionType.RedemptionRefund, cost, "Not enough Stars for the reward.", $"{key}:undo", cancellationToken: cancellationToken);
            await RestoreStockAsync();
            return Result.Failure<RewardRedemptionResult>(RewardErrors.InsufficientStars);
        }

        var generated = await sender.Send(
            new HAMBOX.Modules.Commerce.Application.Features.Promotions.GenerateCoupons.GenerateCouponsCommand(
                reward.CouponPromotionId!.Value,
                new HAMBOX.Modules.Commerce.Application.Contracts.Promotions.GenerateCouponsRequest(
                    1, "STARS", true, 1, 1, DateTime.UtcNow.AddDays(reward.CouponValidDays))),
            cancellationToken);

        if (generated.IsFailure || generated.Value.Count == 0)
        {
            await stars.CreditAsync(userId, PointsTransactionType.RedemptionRefund, cost, "Coupon could not be issued.", $"{key}:refund", cancellationToken: cancellationToken);
            await RestoreStockAsync();
            logger.LogWarning("Coupon reward {RewardId} could not generate a code for user {UserId}; Stars were returned.", reward.Id, userId);
            return Result.Failure<RewardRedemptionResult>(RewardErrors.NotFound);
        }

        redemption.SetCouponCode(generated.Value[0].Code);
        db.RewardRedemptions.Add(redemption);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new RewardRedemptionResult(Guid.Empty, string.Empty, cost, "Completed", redemption.CouponCode));
    }

    private sealed class RewardAbortException : Exception;
}
