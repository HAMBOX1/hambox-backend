using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.GetMyAlertSubscriptions;

public sealed record GetMyAlertSubscriptionsQuery : IRequest<Result<IReadOnlyList<Contracts.Account.CustomerAlertSubscriptionDto>>>;

internal sealed class GetMyAlertSubscriptionsQueryHandler
    : IRequestHandler<GetMyAlertSubscriptionsQuery, Result<IReadOnlyList<Contracts.Account.CustomerAlertSubscriptionDto>>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetMyAlertSubscriptionsQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<Contracts.Account.CustomerAlertSubscriptionDto>>> Handle(
        GetMyAlertSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<IReadOnlyList<Contracts.Account.CustomerAlertSubscriptionDto>>(CommerceErrors.AuthenticationRequired);
        }

        var subscriptions = await _commerceDbContext.CustomerAlertSubscriptions
            .Where(s => s.UserId == _currentUserService.UserId && s.IsActive)
            .OrderByDescending(s => s.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return Result.Success<IReadOnlyList<Contracts.Account.CustomerAlertSubscriptionDto>>(Array.Empty<Contracts.Account.CustomerAlertSubscriptionDto>());
        }

        var productIds = subscriptions.Select(s => s.ProductId).Distinct().ToList();
        var variantIds = subscriptions.Select(s => s.VariantId).Distinct().ToList();

        var products = await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var variantSkus = await _catalogDbContext.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Sku })
            .ToDictionaryAsync(v => v.Id, v => v.Sku, cancellationToken);
        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(_catalogDbContext, productIds, cancellationToken);

        var dtos = subscriptions
            .Select(s =>
            {
                products.TryGetValue(s.ProductId, out var product);
                variantSkus.TryGetValue(s.VariantId, out var sku);
                imageUrls.TryGetValue(s.ProductId, out var imageUrl);
                return AccountMapper.ToAlertSubscriptionDto(s, product?.NameEn ?? "Unknown Product", sku, imageUrl);
            })
            .ToList();

        return Result.Success<IReadOnlyList<Contracts.Account.CustomerAlertSubscriptionDto>>(dtos);
    }
}
