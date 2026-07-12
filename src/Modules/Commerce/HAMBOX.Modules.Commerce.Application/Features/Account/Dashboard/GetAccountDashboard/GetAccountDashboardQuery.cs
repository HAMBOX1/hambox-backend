using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Memberships;
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
    private readonly IMembershipEngine _membershipEngine;
    private readonly MembershipOperationsService _membershipOperations;

    public GetAccountDashboardQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService,
        IMembershipEngine membershipEngine,
        MembershipOperationsService membershipOperations)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
        _membershipEngine = membershipEngine;
        _membershipOperations = membershipOperations;
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

        await EnsureDefaultMembershipAsync(userId, cancellationToken);

        var lifetimeSpend = await _commerceDbContext.Orders
            .Where(o => o.UserId == userId)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        var snapshot = await _membershipEngine.ResolveAsync(userId, cancellationToken);
        var subscription = await _commerceDbContext.MembershipSubscriptions
            .Where(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.Active)
            .OrderByDescending(s => s.ExpiresOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var membershipCard = new MembershipCardDto(
            snapshot.PlanName,
            snapshot.BadgeLabel,
            subscription?.Status.ToString() ?? "Active",
            subscription?.ExpiresOnUtc,
            snapshot.DiscountPercentage,
            snapshot.ReferralMultiplier,
            lifetimeSpend,
            snapshot.Benefits.Select(b => new MembershipBenefitDto(b.Type, b.Value, b.DisplayName, 0)).ToList());

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
            membershipCard,
            wishlistPreview,
            AccountMapper.ToReferralSummaryDto(referralProfile),
            activity,
            unreadCount));
    }

    private async Task EnsureDefaultMembershipAsync(string userId, CancellationToken cancellationToken)
    {
        var hasActive = await _commerceDbContext.MembershipSubscriptions
            .AnyAsync(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.Active, cancellationToken);

        if (hasActive)
        {
            return;
        }

        var defaultPlan = await _commerceDbContext.MembershipPlans
            .FirstOrDefaultAsync(p => p.IsDefault && p.Status == MembershipPlanStatus.Active, cancellationToken);

        if (defaultPlan is null)
        {
            return;
        }

        await _membershipOperations.AssignAsync(userId, defaultPlan.Id, false, userId, cancellationToken);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);
    }
}
