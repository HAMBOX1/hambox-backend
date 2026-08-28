using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.CreateAlertSubscription;

/// <summary>
/// Creates a back-in-stock or price-drop subscription for one variant. Deliberately takes only
/// <see cref="VariantId"/> from the client — <c>ProductId</c> is always resolved server-side from the
/// variant itself, never trusted from the request, per the feature's security requirements.
/// <see cref="GuestSessionId"/> mirrors <c>X-Guest-Cart-Id</c>: reused as-is rather than inventing a
/// second anonymous-identity mechanism, so the same browser session that has a guest cart can also
/// hold guest alert subscriptions, claimed together (conceptually) on login.
/// </summary>
public sealed record CreateAlertSubscriptionCommand(
    Guid VariantId,
    CustomerAlertType AlertType,
    string? GuestSessionId) : IRequest<Result<Contracts.Account.CustomerAlertSubscriptionDto>>;

internal sealed class CreateAlertSubscriptionCommandHandler
    : IRequestHandler<CreateAlertSubscriptionCommand, Result<Contracts.Account.CustomerAlertSubscriptionDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly IFulfillmentRouter _fulfillmentRouter;
    private readonly ICurrentUserService _currentUserService;

    public CreateAlertSubscriptionCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        IInventoryEngine inventoryEngine,
        IFulfillmentRouter fulfillmentRouter,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _inventoryEngine = inventoryEngine;
        _fulfillmentRouter = fulfillmentRouter;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Contracts.Account.CustomerAlertSubscriptionDto>> Handle(
        CreateAlertSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        var isAuthenticated = _currentUserService.IsAuthenticated && _currentUserService.UserId is not null;
        var guestSessionId = string.IsNullOrWhiteSpace(request.GuestSessionId) ? null : request.GuestSessionId;

        if (!isAuthenticated && guestSessionId is null)
        {
            return Result.Failure<Contracts.Account.CustomerAlertSubscriptionDto>(CommerceErrors.AlertSubscriptionOwnerRequired);
        }

        // Server-side resolution — the variant is the actual purchasable entity and the product it
        // belongs to is derived from it, never accepted from the client.
        var variant = await _catalogDbContext.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken);

        if (variant is null || variant.Status == ProductVariantStatus.Archived)
        {
            return Result.Failure<Contracts.Account.CustomerAlertSubscriptionDto>(CatalogErrors.VariantNotFound);
        }

        var product = await _catalogDbContext.Products
            .FirstOrDefaultAsync(p => p.Id == variant.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<Contracts.Account.CustomerAlertSubscriptionDto>(CommerceErrors.ProductNotFound);
        }

        if (product.Status != ProductStatus.Active)
        {
            return Result.Failure<Contracts.Account.CustomerAlertSubscriptionDto>(CatalogErrors.ProductNotActive);
        }

        var userId = isAuthenticated ? _currentUserService.UserId : null;

        var duplicateExists = await _commerceDbContext.CustomerAlertSubscriptions.AnyAsync(
            s => s.IsActive
                && s.AlertType == request.AlertType
                && s.VariantId == request.VariantId
                && (userId != null ? s.UserId == userId : s.GuestSessionId == guestSessionId),
            cancellationToken);

        if (duplicateExists)
        {
            return Result.Failure<Contracts.Account.CustomerAlertSubscriptionDto>(CommerceErrors.AlertSubscriptionExists);
        }

        decimal? lastObservedPrice = null;

        if (request.AlertType == CustomerAlertType.BackInStock)
        {
            // Subscribing to something already purchasable would fire on the very next scan pass —
            // reject cleanly rather than create a subscription with no real "became available"
            // transition. "Already purchasable" uses the same combined manual/supplier-readiness rule
            // as checkout/cart, so a SupplierFirst/SupplierOnly variant that's actually buyable right
            // now (via a READY supplier, zero manual stock) isn't offered a back-in-stock subscription.
            var stock = await _inventoryEngine.GetVariantStockAsync(request.VariantId, cancellationToken);
            var readiness = await _fulfillmentRouter.GetReadinessAsync(request.VariantId, cancellationToken);
            var alreadyAvailable = FulfillmentAvailability.IsAvailable(readiness.Mode, !stock.IsOutOfStock, readiness.SupplierReady);
            if (alreadyAvailable && variant.Status == ProductVariantStatus.Active && variant.IsVisible)
            {
                return Result.Failure<Contracts.Account.CustomerAlertSubscriptionDto>(CommerceErrors.VariantAlreadyAvailable);
            }
        }
        else
        {
            decimal? supplierEffectivePrice = null;
            if (variant.FulfillmentMode is FulfillmentMode.SupplierFirst or FulfillmentMode.SupplierOnly)
            {
                var overrides = await _fulfillmentRouter.GetEffectivePriceOverridesBulkAsync([variant.Id], cancellationToken);
                supplierEffectivePrice = overrides.TryGetValue(variant.Id, out var overridePrice) ? overridePrice : null;
            }

            lastObservedPrice = EffectivePriceResolver.Resolve(variant, product, supplierEffectivePrice);
        }

        var subscription = userId is not null
            ? CustomerAlertSubscription.CreateForUser(userId, request.AlertType, request.VariantId, product.Id, lastObservedPrice)
            : CustomerAlertSubscription.CreateForGuest(guestSessionId!, request.AlertType, request.VariantId, product.Id, lastObservedPrice);

        _commerceDbContext.CustomerAlertSubscriptions.Add(subscription);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext, [product.Id], cancellationToken);
        imageUrls.TryGetValue(product.Id, out var imageUrl);

        return Result.Success(AccountMapper.ToAlertSubscriptionDto(subscription, product.NameEn, variant.Sku, imageUrl));
    }
}
