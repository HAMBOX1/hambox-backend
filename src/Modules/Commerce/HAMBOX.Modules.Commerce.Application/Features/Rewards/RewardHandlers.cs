using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Application.Rewards;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Rewards;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Rewards;

// Stars Shop (RW-*): customer catalog / redeem / goals, plus the admin catalog and redemption tracking.
// Collapsed into one file like the other loyalty handler sets.

public sealed record RewardCatalogItemDto(
    Guid Id,
    Guid ProductId,
    string NameEn,
    string NameAr,
    string? ImageUrl,
    int StarsPrice,
    decimal ValueUsd,
    bool InStock,
    string? LockReason,
    bool IsGoal,
    int MissingStars,
    string? Platform,
    string? Edition,
    string? Region,
    string? Summary,
    string DeliveryMode,
    bool RequiresElite,
    string? MinRankKey,
    bool IsCoupon = false,
    int? WasStarsPrice = null);

public sealed record RewardCatalogDto(bool Enabled, int Available, IReadOnlyList<RewardCatalogItemDto> Items);

public sealed record RewardRedemptionDto(
    Guid Id, Guid RewardItemId, string ProductName, Guid OrderId, string? OrderNumber, int StarsSpent, string Source,
    string Status, string OrderStatus, DateTimeOffset RedeemedOnUtc, string? CouponCode = null);

public sealed record AdminRewardDto(
    Guid Id, Guid ProductId, Guid ProductVariantId, string ProductName, string VariantSku, int StarsPrice, int? RemainingQuantity,
    int? MaxPerUser, string? MinRankKey, bool RequiresElite, int SortOrder, bool IsActive, DateTimeOffset? StartsOnUtc,
    DateTimeOffset? EndsOnUtc, string FulfillmentMode, bool IsGiftOption = false, Guid? CouponPromotionId = null, int CouponValidDays = 30,
    string? TitleEn = null, string? TitleAr = null, int? SaleStarsPrice = null, DateTimeOffset? SaleStartsOnUtc = null, DateTimeOffset? SaleEndsOnUtc = null);

public sealed record RewardInput(
    Guid ProductId, Guid ProductVariantId, int StarsPrice, int? RemainingQuantity, int? MaxPerUser, string? MinRankKey,
    bool RequiresElite, int SortOrder, bool IsActive, DateTimeOffset? StartsOnUtc, DateTimeOffset? EndsOnUtc,
    bool IsGiftOption = false,
    Guid? CouponPromotionId = null,
    int CouponValidDays = 30,
    string? TitleEn = null,
    string? TitleAr = null,
    int? SaleStarsPrice = null,
    DateTimeOffset? SaleStartsOnUtc = null,
    DateTimeOffset? SaleEndsOnUtc = null);

internal sealed class RewardInputValidator : AbstractValidator<RewardInput>
{
    public RewardInputValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().When(x => x.CouponPromotionId is null);
        RuleFor(x => x.ProductVariantId).NotEmpty().When(x => x.CouponPromotionId is null);
        RuleFor(x => x.TitleEn).NotEmpty().MaximumLength(200).When(x => x.CouponPromotionId is not null);
        RuleFor(x => x.TitleAr).MaximumLength(200);
        RuleFor(x => x.CouponValidDays).InclusiveBetween(1, 3650);
        RuleFor(x => x.SaleStarsPrice).InclusiveBetween(1, 100_000_000).When(x => x.SaleStarsPrice.HasValue);
        RuleFor(x => x.StarsPrice).InclusiveBetween(1, 100_000_000);
        RuleFor(x => x.RemainingQuantity).InclusiveBetween(0, 10_000_000).When(x => x.RemainingQuantity.HasValue);
        RuleFor(x => x.MaxPerUser).InclusiveBetween(1, 1000).When(x => x.MaxPerUser.HasValue);
        RuleFor(x => x.MinRankKey).MaximumLength(30);
        RuleFor(x => x.EndsOnUtc).GreaterThan(x => x.StartsOnUtc).When(x => x.StartsOnUtc.HasValue && x.EndsOnUtc.HasValue);
    }
}

// ---- Customer ----------------------------------------------------------------------------------------------------

public sealed record GetRewardCatalogQuery : IRequest<Result<RewardCatalogDto>>;

internal sealed class GetRewardCatalogQueryHandler(
    ICommerceDbContext db, ICatalogDbContext catalog, IInventoryEngine inventory, ICurrentUserService currentUser,
    StarsLedgerService ledger, RewardRedemptionService redemption)
    : IRequestHandler<GetRewardCatalogQuery, Result<RewardCatalogDto>>
{
    public async Task<Result<RewardCatalogDto>> Handle(GetRewardCatalogQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<RewardCatalogDto>(CommerceErrors.AuthenticationRequired);
        }

        if (!await redemption.IsShopEnabledAsync(cancellationToken))
        {
            return Result.Success(new RewardCatalogDto(false, 0, []));
        }

        var now = DateTimeOffset.UtcNow;
        var rewards = (await db.RewardItems.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(cancellationToken))
            .Where(r => r.IsLiveAt(now)).ToList();
        var balance = await ledger.GetBalanceAsync(userId, cancellationToken);
        var goals = (await db.StarsGoals.AsNoTracking().Where(g => g.UserId == userId).Select(g => g.RewardItemId).ToListAsync(cancellationToken)).ToHashSet();

        var productIds = rewards.Where(r => !r.IsCoupon).Select(r => r.ProductId).Distinct().ToList();
        var variantIds = rewards.Where(r => !r.IsCoupon).Select(r => r.ProductVariantId).Distinct().ToList();
        var products = await catalog.Products.AsNoTracking().Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var variants = await catalog.ProductVariants.AsNoTracking().Include(v => v.SelectedOptions)
            .Where(v => variantIds.Contains(v.Id) && v.Status == ProductVariantStatus.Active)
            .ToDictionaryAsync(v => v.Id, cancellationToken);
        IReadOnlyDictionary<Guid, VariantStockSnapshot> stock = variantIds.Count > 0
            ? await inventory.GetVariantStockBulkAsync(variantIds, cancellationToken)
            : new Dictionary<Guid, VariantStockSnapshot>();
        var images = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(catalog, productIds, cancellationToken);
        var labels = await RewardOptionLabels.LoadAsync(catalog, productIds, variants.Values, cancellationToken);

        var items = new List<RewardCatalogItemDto>();
        foreach (var r in rewards)
        {
            var price = r.PriceAt(now);
            var was = price < r.StarsPrice ? r.StarsPrice : (int?)null;
            var lockReason = await redemption.LockReasonAsync(r, userId, cancellationToken);

            if (r.IsCoupon)
            {
                items.Add(new RewardCatalogItemDto(
                    r.Id, Guid.Empty, r.TitleEn ?? "Coupon", r.TitleAr ?? r.TitleEn ?? "كوبون", null, price, 0m,
                    r.RemainingQuantity is null or > 0, lockReason, goals.Contains(r.Id), Math.Max(price - balance.Available, 0),
                    null, null, null, null, "Coupon", r.RequiresElite, r.MinRankKey, true, was));
                continue;
            }

            if (!products.TryGetValue(r.ProductId, out var product) || !variants.TryGetValue(r.ProductVariantId, out var variant))
            {
                continue;
            }

            var manual = variant.FulfillmentMode is FulfillmentMode.ManualOnly or FulfillmentMode.ManualFirst;
            var hasStock = r.RemainingQuantity is null or > 0
                && (!manual || (stock.TryGetValue(variant.Id, out var s) && s.Available > 0) || variant.FulfillmentMode == FulfillmentMode.ManualFirst);
            var (platform, edition, region, summary) = labels.GetValueOrDefault(variant.Id);

            items.Add(new RewardCatalogItemDto(
                r.Id, product.Id, product.NameEn, product.NameAr, images.GetValueOrDefault(product.Id), price,
                variant.PriceOverride ?? product.Price, hasStock, lockReason,
                goals.Contains(r.Id), Math.Max(price - balance.Available, 0), platform, edition, region, summary,
                variant.FulfillmentMode.ToString(), r.RequiresElite, r.MinRankKey, false, was));
        }

        return Result.Success(new RewardCatalogDto(true, balance.Available, items));
    }
}

public sealed record RedeemRewardCommand(Guid RewardItemId, string? Country, string Email) : IRequest<Result<RewardRedemptionResult>>;

internal sealed class RedeemRewardCommandHandler(
    RewardRedemptionService redemption, ICurrentUserService currentUser)
    : IRequestHandler<RedeemRewardCommand, Result<RewardRedemptionResult>>
{
    public async Task<Result<RewardRedemptionResult>> Handle(RedeemRewardCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<RewardRedemptionResult>(CommerceErrors.AuthenticationRequired);
        }

        return await redemption.RedeemAsync(
            userId, request.Email, string.IsNullOrWhiteSpace(request.Country) ? "US" : request.Country!.Trim().ToUpperInvariant(),
            request.RewardItemId, RewardRedemptionSource.StarsShop, null, cancellationToken);
    }
}

public sealed record GetMyRedemptionsQuery : IRequest<Result<IReadOnlyList<RewardRedemptionDto>>>;

internal sealed class GetMyRedemptionsQueryHandler(ICommerceDbContext db, ICatalogDbContext catalog, ICurrentUserService currentUser)
    : IRequestHandler<GetMyRedemptionsQuery, Result<IReadOnlyList<RewardRedemptionDto>>>
{
    public async Task<Result<IReadOnlyList<RewardRedemptionDto>>> Handle(GetMyRedemptionsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<RewardRedemptionDto>>(CommerceErrors.AuthenticationRequired);
        }

        return Result.Success(await RewardRedemptionQueries.ListAsync(db, catalog, db.RewardRedemptions.Where(r => r.UserId == userId), 100, cancellationToken));
    }
}

public sealed record SetStarsGoalCommand(Guid RewardItemId, bool Saved) : IRequest<Result>;

internal sealed class SetStarsGoalCommandHandler(ICommerceDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<SetStarsGoalCommand, Result>
{
    public async Task<Result> Handle(SetStarsGoalCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var existing = await db.StarsGoals.FirstOrDefaultAsync(g => g.UserId == userId && g.RewardItemId == request.RewardItemId, cancellationToken);
        if (request.Saved && existing is null)
        {
            if (!await db.RewardItems.AnyAsync(r => r.Id == request.RewardItemId, cancellationToken))
            {
                return Result.Failure(RewardErrors.NotFound);
            }

            db.StarsGoals.Add(StarsGoal.Create(userId, request.RewardItemId));
        }
        else if (!request.Saved && existing is not null)
        {
            db.StarsGoals.Remove(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---- Admin -------------------------------------------------------------------------------------------------------

public sealed record GetAdminRewardsQuery : IRequest<Result<IReadOnlyList<AdminRewardDto>>>;

internal sealed class GetAdminRewardsQueryHandler(ICommerceDbContext db, ICatalogDbContext catalog)
    : IRequestHandler<GetAdminRewardsQuery, Result<IReadOnlyList<AdminRewardDto>>>
{
    public async Task<Result<IReadOnlyList<AdminRewardDto>>> Handle(GetAdminRewardsQuery request, CancellationToken cancellationToken)
    {
        var rewards = await db.RewardItems.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(cancellationToken);
        var pIds = rewards.Select(r => r.ProductId).ToList();
        var vIds = rewards.Select(r => r.ProductVariantId).ToList();
        var names = await catalog.Products.AsNoTracking().Where(p => pIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);
        var variants = await catalog.ProductVariants.AsNoTracking().Where(v => vIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);

        return Result.Success<IReadOnlyList<AdminRewardDto>>(rewards.Select(r => new AdminRewardDto(
            r.Id, r.ProductId, r.ProductVariantId, r.IsCoupon ? r.TitleEn ?? "Coupon" : names.GetValueOrDefault(r.ProductId, "?"),
            variants.TryGetValue(r.ProductVariantId, out var v) ? v.Sku : "—", r.StarsPrice, r.RemainingQuantity, r.MaxPerUser,
            r.MinRankKey, r.RequiresElite, r.SortOrder, r.IsActive, r.StartsOnUtc, r.EndsOnUtc,
            r.IsCoupon ? "Coupon" : v?.FulfillmentMode.ToString() ?? "?", r.IsGiftOption, r.CouponPromotionId, r.CouponValidDays,
            r.TitleEn, r.TitleAr, r.SaleStarsPrice, r.SaleStartsOnUtc, r.SaleEndsOnUtc)).ToList());
    }
}

public sealed record SaveRewardCommand(Guid? Id, RewardInput Input) : IRequest<Result<Guid>>;

internal sealed class SaveRewardCommandValidator : AbstractValidator<SaveRewardCommand>
{
    public SaveRewardCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new RewardInputValidator());
}

internal sealed class SaveRewardCommandHandler(ICommerceDbContext db, ICatalogDbContext catalog)
    : IRequestHandler<SaveRewardCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveRewardCommand request, CancellationToken cancellationToken)
    {
        var i = request.Input;
        var coupon = i.CouponPromotionId is not null;
        if (coupon)
        {
            // A coupon reward must point at a real coupon campaign.
            if (!await db.Promotions.AnyAsync(p => p.Id == i.CouponPromotionId, cancellationToken))
            {
                return Result.Failure<Guid>(CommerceErrors.PromotionNotFound);
            }
        }
        else if (!await catalog.ProductVariants.AnyAsync(v => v.Id == i.ProductVariantId && v.ProductId == i.ProductId, cancellationToken))
        {
            return Result.Failure<Guid>(RewardErrors.NotFound);
        }

        var productId = coupon ? Guid.Empty : i.ProductId;
        var variantId = coupon ? Guid.Empty : i.ProductVariantId;

        RewardItem? reward;
        if (request.Id is { } id)
        {
            reward = await db.RewardItems.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (reward is null)
            {
                return Result.Failure<Guid>(RewardErrors.NotFound);
            }

            reward.Apply(i.StarsPrice, i.RemainingQuantity, i.MaxPerUser, i.MinRankKey, i.RequiresElite, i.SortOrder, i.IsActive, i.StartsOnUtc, i.EndsOnUtc);
        }
        else
        {
            reward = RewardItem.Create(
                productId, variantId, i.StarsPrice, i.RemainingQuantity, i.MaxPerUser, i.MinRankKey, i.RequiresElite,
                i.SortOrder, i.IsActive, i.StartsOnUtc, i.EndsOnUtc);
            db.RewardItems.Add(reward);
        }

        reward.SetGiftOption(!coupon && i.IsGiftOption);
        reward.SetCoupon(i.CouponPromotionId, i.CouponValidDays, i.TitleEn, i.TitleAr);
        reward.SetSale(i.SaleStarsPrice, i.SaleStartsOnUtc, i.SaleEndsOnUtc);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(reward.Id);
    }
}

public sealed record DeleteRewardCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteRewardCommandHandler(ICommerceDbContext db) : IRequestHandler<DeleteRewardCommand, Result>
{
    public async Task<Result> Handle(DeleteRewardCommand request, CancellationToken cancellationToken)
    {
        var reward = await db.RewardItems.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (reward is null)
        {
            return Result.Failure(RewardErrors.NotFound);
        }

        db.RewardItems.Remove(reward);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetAdminRedemptionsQuery(string? Status) : IRequest<Result<IReadOnlyList<RewardRedemptionDto>>>;

internal sealed class GetAdminRedemptionsQueryHandler(ICommerceDbContext db, ICatalogDbContext catalog)
    : IRequestHandler<GetAdminRedemptionsQuery, Result<IReadOnlyList<RewardRedemptionDto>>>
{
    public async Task<Result<IReadOnlyList<RewardRedemptionDto>>> Handle(GetAdminRedemptionsQuery request, CancellationToken cancellationToken)
    {
        var query = db.RewardRedemptions.AsQueryable();
        if (Enum.TryParse<RewardRedemptionStatus>(request.Status, true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        return Result.Success(await RewardRedemptionQueries.ListAsync(db, catalog, query, 200, cancellationToken));
    }
}

internal static class RewardRedemptionQueries
{
    public static async Task<IReadOnlyList<RewardRedemptionDto>> ListAsync(
        ICommerceDbContext db, ICatalogDbContext catalog, IQueryable<RewardRedemption> query, int take, CancellationToken cancellationToken)
    {
        var rows = await query.AsNoTracking().OrderByDescending(r => r.RedeemedOnUtc).Take(take).ToListAsync(cancellationToken);
        var rewardIds = rows.Select(r => r.RewardItemId).Distinct().ToList();
        var orderIds = rows.Select(r => r.OrderId).Distinct().ToList();
        var rewards = await db.RewardItems.AsNoTracking().Where(r => rewardIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        var productIds = rewards.Values.Where(r => !r.IsCoupon).Select(r => r.ProductId).Distinct().ToList();
        var names = await catalog.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);
        var orders = await db.Orders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).Select(o => new { o.Id, o.OrderNumber, o.Status }).ToDictionaryAsync(o => o.Id, cancellationToken);

        return rows.Select(r =>
        {
            rewards.TryGetValue(r.RewardItemId, out var reward);
            var name = reward is null ? "?" : reward.IsCoupon ? reward.TitleEn ?? "Coupon" : names.GetValueOrDefault(reward.ProductId, "?");
            orders.TryGetValue(r.OrderId, out var order);

            // The order is the source of truth for delivery; a coupon has no order and is complete once the code exists.
            var status = r.CouponCode is not null || order?.Status == OrderStatus.Completed ? "Completed"
                : order?.Status is OrderStatus.Failed or OrderStatus.Cancelled or OrderStatus.Refunded ? "Failed"
                : "Processing";

            return new RewardRedemptionDto(
                r.Id, r.RewardItemId, name, r.OrderId, order?.OrderNumber, r.StarsSpent, r.Source.ToString(), status,
                order?.Status.ToString() ?? (r.CouponCode is not null ? "Coupon" : "?"), r.RedeemedOnUtc, r.CouponCode);
        }).ToList();
    }
}
