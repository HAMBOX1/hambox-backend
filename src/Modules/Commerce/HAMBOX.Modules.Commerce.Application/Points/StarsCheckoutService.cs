using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;

namespace HAMBOX.Modules.Commerce.Application.Points;

public sealed record StarsRedemptionQuote(int Stars, decimal DiscountUsd, int Available, string? Problem)
{
    public static StarsRedemptionQuote None { get; } = new(0, 0m, 0, null);
}

/// <summary>
/// "Pay part of this order with Stars" (ST-05 / RW-02). A request is turned into a quote: the Stars actually usable and the
/// USD discount they buy, honouring the configured rate, minimum, maximum share of the order, and the customer's real
/// available balance. Stars never cover a whole order here (the full-Stars path is the Stars Shop), and they are
/// not a cash value — the discount is applied after promotions/tax and recorded as a reservation on the ledger.
/// </summary>
public sealed class StarsCheckoutService(
    IPlatformSettingsProvider settings, ICurrentUserService currentUser, StarsLedgerService ledger,
    HAMBOX.Modules.Commerce.Application.Memberships.PriceSlotService? priceSlots = null)
{
    /// <summary>The share of an order Stars may ever cover; the brief forbids 100% on the general checkout.</summary>
    private const int HardMaxPercent = 99;

    public async Task<StarsRedemptionQuote> QuoteAsync(int requestedStars, decimal orderTotalUsd, CancellationToken cancellationToken)
    {
        if (requestedStars <= 0 || orderTotalUsd <= 0m || !currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return StarsRedemptionQuote.None;
        }

        var features = await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken);
        var config = await settings.GetAsync<PurchasePointsSettingsPayload>(PlatformSettingsCategoryKeys.PurchasePoints, cancellationToken);
        if (!features.StarsShop || config.RedeemStarsPerUsd <= 0m)
        {
            return StarsRedemptionQuote.None;
        }

        var available = (await ledger.GetBalanceAsync(userId, cancellationToken)).Available;
        var percent = Math.Min(config.RedeemMaxPercent, HardMaxPercent);
        var capUsd = Math.Floor(orderTotalUsd * percent) / 100m;
        var usable = (int)Math.Min(Math.Min(requestedStars, available), Math.Floor(capUsd * config.RedeemStarsPerUsd));

        if (usable <= 0 || usable < config.MinRedeemStars)
        {
            return new StarsRedemptionQuote(0, 0m, available, usable <= 0 ? null : "BelowMinimum");
        }

        var discount = Math.Floor(usable * 100m / config.RedeemStarsPerUsd) / 100m;
        var stars = (int)Math.Ceiling(discount * config.RedeemStarsPerUsd);
        return new StarsRedemptionQuote(Math.Min(stars, usable), discount, available, null);
    }

    /// <summary>Called right after an order is created: holds the Stars the order's totals were discounted by.</summary>
    public async Task ReserveAsync(Order order, PromotionEvaluationResult evaluation, CancellationToken cancellationToken)
    {
        if (priceSlots is not null)
        {
            await priceSlots.RecordAsync(order, cancellationToken);
        }

        if (evaluation.StarsRedeemed <= 0)
        {
            return;
        }

        if (!await ledger.ReserveForOrderAsync(
                order.UserId, evaluation.StarsRedeemed, order.Id, order.OrderNumber, $"Stars used on order {order.OrderNumber}.", cancellationToken))
        {
            throw new InvalidOperationException("Not enough available Stars to cover this order's Stars discount.");
        }
    }
}
