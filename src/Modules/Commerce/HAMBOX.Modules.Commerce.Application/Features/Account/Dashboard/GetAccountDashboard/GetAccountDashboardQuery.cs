using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Dashboard.GetAccountDashboard;

public sealed record GetAccountDashboardQuery() : IRequest<Result<AccountDashboardDto>>;

internal sealed class GetAccountDashboardQueryHandler : IRequestHandler<GetAccountDashboardQuery, Result<AccountDashboardDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetAccountDashboardQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<AccountDashboardDto>> Handle(
        GetAccountDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<AccountDashboardDto>(CommerceErrors.AuthenticationRequired);
        }

        var userId = _currentUserService.UserId;

        var lifetimeSpend = await _commerceDbContext.Orders
            .Where(o => o.UserId == userId)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        var (tier, nextThreshold, progress) = MembershipTierResolver.Resolve(lifetimeSpend);

        var wishlistItems = await _commerceDbContext.WishlistItems
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedOnUtc)
            .Take(4)
            .ToListAsync(cancellationToken);

        var wishlistProductIds = wishlistItems.Select(w => w.ProductId).ToList();
        var products = await _catalogDbContext.Products
            .Where(p => wishlistProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext,
            wishlistProductIds,
            cancellationToken);

        var wishlistPreview = wishlistItems
            .Where(w => products.ContainsKey(w.ProductId))
            .Select(w =>
            {
                imageUrls.TryGetValue(w.ProductId, out var imageUrl);
                return new WishlistPreviewItemDto(
                    w.ProductId,
                    products[w.ProductId].NameEn,
                    products[w.ProductId].Price,
                    imageUrl,
                    w.AddedOnUtc);
            })
            .ToList();

        var referralProfile = await _commerceDbContext.ReferralProfiles
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (referralProfile is null)
        {
            referralProfile = ReferralProfile.CreateForUser(userId);
            _commerceDbContext.ReferralProfiles.Add(referralProfile);
            await _commerceDbContext.SaveChangesAsync(cancellationToken);
        }

        var unreadCount = await _commerceDbContext.UserNotifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        var recentOrders = await _commerceDbContext.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedOnUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        var recentNotifications = await _commerceDbContext.UserNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedOnUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        var activity = recentOrders
            .Select(o => new AccountActivityItemDto(
                "Order",
                o.OrderNumber,
                $"Order total ${o.TotalAmount:F2}",
                o.CreatedOnUtc))
            .Concat(recentNotifications.Select(n => new AccountActivityItemDto(
                n.Category,
                n.Title,
                n.Body,
                n.CreatedOnUtc)))
            .OrderByDescending(a => a.OccurredOnUtc)
            .Take(6)
            .ToList();

        return Result.Success(new AccountDashboardDto(
            new MembershipCardDto(tier, lifetimeSpend, nextThreshold, progress),
            wishlistPreview,
            AccountMapper.ToReferralSummaryDto(referralProfile),
            activity,
            unreadCount));
    }
}
