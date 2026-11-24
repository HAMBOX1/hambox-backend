using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Membership;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Rewards;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Rewards;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Memberships.Gift;

// ELITE monthly gift (EL-06) and the members-only Vault. Collapsed into one file like the other loyalty handler sets.

public sealed record GiftOptionDto(Guid RewardId, Guid ProductId, string NameEn, string NameAr, string? ImageUrl, bool InStock);

public sealed record MonthlyGiftDto(
    bool Enabled, bool IsMember, int ClaimsAllowed, int ClaimsUsed, string PeriodKey, IReadOnlyList<GiftOptionDto> Options);

public sealed record VaultProductDto(Guid ProductId, string NameEn, string NameAr, decimal Price, string? ImageUrl);

public sealed record VaultDto(IReadOnlyList<VaultProductDto> Products);

internal static class EliteBenefits
{
    /// <summary>The numeric value of the member's plan benefit of this type, or null when they have no active ELITE plan or the benefit.</summary>
    public static async Task<string?> ValueAsync(
        IMembershipEngine engine, IMembershipAccessProvider access, string userId, string type, CancellationToken cancellationToken)
    {
        if (!(await access.GetAccessInfoAsync(userId, cancellationToken)).SubscriptionId.HasValue)
        {
            return null;
        }

        return (await engine.ResolveAsync(userId, cancellationToken)).Benefits
            .FirstOrDefault(b => b.Type.Equals(type, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    public static string Period(DateTime utcNow) => utcNow.ToString("yyyy-MM");
}

// ---- Monthly gift ------------------------------------------------------------------------------------------------

public sealed record GetMonthlyGiftQuery : IRequest<Result<MonthlyGiftDto>>;

internal sealed class GetMonthlyGiftQueryHandler(
    ICommerceDbContext db, ICatalogDbContext catalog, IInventoryEngine inventory, ICurrentUserService currentUser,
    IMembershipEngine engine, IMembershipAccessProvider access, IPlatformSettingsProvider settings)
    : IRequestHandler<GetMonthlyGiftQuery, Result<MonthlyGiftDto>>
{
    public async Task<Result<MonthlyGiftDto>> Handle(GetMonthlyGiftQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<MonthlyGiftDto>(CommerceErrors.AuthenticationRequired);
        }

        var period = EliteBenefits.Period(DateTime.UtcNow);
        var features = await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken);
        var value = features.EliteSubscription && features.EliteVault
            ? await EliteBenefits.ValueAsync(engine, access, userId, nameof(Domain.Memberships.MembershipBenefitType.MonthlyGift), cancellationToken)
            : null;
        var allowed = int.TryParse(value, out var n) ? Math.Max(n, 0) : 0;
        var used = await db.RewardRedemptions.CountAsync(
            r => r.UserId == userId && r.Source == RewardRedemptionSource.EliteMonthlyGift && r.PeriodKey!.StartsWith(period), cancellationToken);

        var options = new List<GiftOptionDto>();
        if (allowed > 0)
        {
            var now = DateTimeOffset.UtcNow;
            var rewards = (await db.RewardItems.AsNoTracking().Where(r => r.IsActive && r.IsGiftOption).OrderBy(r => r.SortOrder).ToListAsync(cancellationToken))
                .Where(r => r.IsLiveAt(now)).ToList();
            var pIds = rewards.Select(r => r.ProductId).ToList();
            var vIds = rewards.Select(r => r.ProductVariantId).ToList();
            var products = await catalog.Products.AsNoTracking().Where(p => pIds.Contains(p.Id) && p.Status == ProductStatus.Active).ToDictionaryAsync(p => p.Id, cancellationToken);
            var stock = vIds.Count > 0 ? await inventory.GetVariantStockBulkAsync(vIds, cancellationToken) : new Dictionary<Guid, VariantStockSnapshot>();
            var images = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(catalog, pIds, cancellationToken);

            foreach (var r in rewards.Where(r => products.ContainsKey(r.ProductId)))
            {
                var p = products[r.ProductId];
                var inStock = r.RemainingQuantity is null or > 0 && stock.TryGetValue(r.ProductVariantId, out var s) && s.Available > 0;
                options.Add(new GiftOptionDto(r.Id, p.Id, p.NameEn, p.NameAr, images.GetValueOrDefault(p.Id), inStock));
            }
        }

        return Result.Success(new MonthlyGiftDto(features.EliteVault, allowed > 0, allowed, used, period, options));
    }
}

public sealed record ClaimMonthlyGiftCommand(Guid RewardId, string? Country, string Email) : IRequest<Result<RewardRedemptionResult>>;

internal sealed class ClaimMonthlyGiftCommandHandler(
    ICommerceDbContext db, ICurrentUserService currentUser, IMembershipEngine engine, IMembershipAccessProvider access,
    IPlatformSettingsProvider settings, RewardRedemptionService redemption)
    : IRequestHandler<ClaimMonthlyGiftCommand, Result<RewardRedemptionResult>>
{
    public async Task<Result<RewardRedemptionResult>> Handle(ClaimMonthlyGiftCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<RewardRedemptionResult>(CommerceErrors.AuthenticationRequired);
        }

        var features = await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken);
        var value = features.EliteSubscription && features.EliteVault
            ? await EliteBenefits.ValueAsync(engine, access, userId, nameof(Domain.Memberships.MembershipBenefitType.MonthlyGift), cancellationToken)
            : null;
        if (!int.TryParse(value, out var allowed) || allowed < 1)
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.GiftNotAvailable);
        }

        if (!await db.RewardItems.AnyAsync(r => r.Id == request.RewardId && r.IsGiftOption, cancellationToken))
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.NotFound);
        }

        var period = EliteBenefits.Period(DateTime.UtcNow);
        var used = await db.RewardRedemptions.CountAsync(
            r => r.UserId == userId && r.Source == RewardRedemptionSource.EliteMonthlyGift && r.PeriodKey!.StartsWith(period), cancellationToken);
        if (used >= allowed)
        {
            return Result.Failure<RewardRedemptionResult>(RewardErrors.GiftAlreadyClaimed);
        }

        // The (user, period key) unique index makes a concurrent second claim of the same slot fail even if both pass the count above.
        return await redemption.RedeemAsync(
            userId, request.Email, string.IsNullOrWhiteSpace(request.Country) ? "US" : request.Country!.Trim().ToUpperInvariant(),
            request.RewardId, RewardRedemptionSource.EliteMonthlyGift, $"{period}#{used + 1}", cancellationToken);
    }
}

// ---- Vault -------------------------------------------------------------------------------------------------------

public sealed record GetVaultQuery : IRequest<Result<VaultDto>>;

internal sealed class GetVaultQueryHandler(
    ICommerceDbContext db, ICatalogDbContext catalog, ICurrentUserService currentUser, IMembershipEngine engine,
    IMembershipAccessProvider access, IPlatformSettingsProvider settings)
    : IRequestHandler<GetVaultQuery, Result<VaultDto>>
{
    public async Task<Result<VaultDto>> Handle(GetVaultQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<VaultDto>(CommerceErrors.AuthenticationRequired);
        }

        var features = await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken);
        var vault = features.EliteSubscription && features.EliteVault
            ? await EliteBenefits.ValueAsync(engine, access, userId, nameof(Domain.Memberships.MembershipBenefitType.VaultAccess), cancellationToken)
            : null;

        // Enforced here, on the server: a non-member who knows the URL gets nothing, not just a hidden button.
        if (!string.Equals(vault, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<VaultDto>(CommerceErrors.VaultMembersOnly);
        }

        var planId = (await engine.ResolveAsync(userId, cancellationToken)).PlanId;
        var ids = await db.MembershipPlanProductAccess.AsNoTracking().Where(a => a.PlanId == planId).Select(a => a.ProductId).ToListAsync(cancellationToken);
        var products = await catalog.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.Status == ProductStatus.Active).ToListAsync(cancellationToken);
        var images = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(catalog, products.Select(p => p.Id).ToList(), cancellationToken);

        return Result.Success(new VaultDto(products
            .Select(p => new VaultProductDto(p.Id, p.NameEn, p.NameAr, p.Price, images.GetValueOrDefault(p.Id))).ToList()));
    }
}
