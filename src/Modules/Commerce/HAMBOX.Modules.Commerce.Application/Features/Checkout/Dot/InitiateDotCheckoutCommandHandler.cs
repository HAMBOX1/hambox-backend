using System.Security.Cryptography;
using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Membership;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

internal sealed class InitiateDotCheckoutCommandHandler(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ICurrentUserService currentUserService,
    IInventoryEngine inventoryEngine,
    CartResponseBuilder cartResponseBuilder,
    CartLineValidator cartLineValidator,
    IMembershipAccessProvider membershipAccess,
    IDotPricePointResolver pricePointResolver,
    IDotPaymentGateway dotGateway,
    IOptions<DotSettings> dotOptions,
    IPlatformSettingsProvider platformSettings,
    ILogger<InitiateDotCheckoutCommandHandler> logger)
    : IRequestHandler<InitiateDotCheckoutCommand, Result<DotCheckoutInitiationDto>>
{
    public async Task<Result<DotCheckoutInitiationDto>> Handle(
        InitiateDotCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<DotCheckoutInitiationDto>(CommerceErrors.AuthenticationRequired);
        }

        var cart = await commerceDbContext.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == currentUserService.UserId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Failure<DotCheckoutInitiationDto>(CommerceErrors.CartEmpty);
        }

        var access = await membershipAccess.GetAccessInfoAsync(currentUserService.UserId, cancellationToken);
        if (access.MaxPurchasesPerMonth is int monthlyLimit)
        {
            var startOfMonthUtc = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var purchasesThisMonth = await commerceDbContext.Orders.CountAsync(
                o => o.UserId == currentUserService.UserId
                    && o.Status == OrderStatus.Completed
                    && o.CreatedOnUtc >= startOfMonthUtc,
                cancellationToken);

            if (purchasesThisMonth >= monthlyLimit)
            {
                return Result.Failure<DotCheckoutInitiationDto>(CommerceErrors.MembershipPurchaseLimitExceeded(monthlyLimit));
            }
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var variantIds = cart.Items.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variants = variantIds.Count > 0
            ? await catalogDbContext.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken)
            : new Dictionary<Guid, HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant>();

        var variantStock = variantIds.Count > 0
            ? await inventoryEngine.GetVariantStockBulkAsync(variantIds, cancellationToken)
            : new Dictionary<Guid, VariantStockSnapshot>();

        var productAccess = await membershipAccess.GetProductsAccessAsync(
            currentUserService.UserId, productIds, cancellationToken);

        var lineValidation = await cartLineValidator.ValidateAsync(
            cart, products, variants, variantStock, productAccess, access, cancellationToken);
        if (lineValidation.IsFailure)
        {
            return Result.Failure<DotCheckoutInitiationDto>(lineValidation.Error);
        }

        var (subtotal, discountAmount, taxAmount, totalAmount, evaluation) =
            await cartResponseBuilder.BuildOrderAmountsAsync(cart, request.Country, cancellationToken);

        if (evaluation.ValidationErrors.Count > 0)
        {
            return Result.Failure<DotCheckoutInitiationDto>(
                CommerceErrors.InvalidCoupon(string.Join(' ', evaluation.ValidationErrors)));
        }

        var chargeAmountResult = await pricePointResolver.ResolveAsync(totalAmount, request.Country, cancellationToken);
        if (chargeAmountResult.IsFailure)
        {
            return Result.Failure<DotCheckoutInitiationDto>(chargeAmountResult.Error);
        }

        var settings = dotOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.PartnerId)
            || string.IsNullOrWhiteSpace(settings.ServiceId)
            || string.IsNullOrWhiteSpace(settings.PublicRedirectUrl))
        {
            logger.LogError("DOT checkout attempted but DOT configuration is incomplete.");
            return Result.Failure<DotCheckoutInitiationDto>(CommerceErrors.DotGatewayMisconfigured);
        }

        // Guaranteed to parse — InitiateDotCheckoutCommandValidator only lets a valid
        // DotWalletOperator member name through before this handler ever runs.
        var wallet = Enum.Parse<DotWalletOperator>(request.Wallet, ignoreCase: true);

        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var orderItems = cart.Items
            .Select(item =>
            {
                var variantId = item.ProductVariantId;
                string? sku = null;
                if (variantId is not null && variants.TryGetValue(variantId.Value, out var variant))
                {
                    sku = variant.Sku;
                }

                return (
                    item.ProductId,
                    products[item.ProductId].NameEn,
                    item.Quantity,
                    item.UnitPrice,
                    variantId,
                    sku);
            })
            .ToList();

        var order = Order.Create(
            currentUserService.UserId!,
            orderNumber,
            request.Email,
            request.Country,
            "dot",
            subtotal,
            discountAmount,
            taxAmount,
            totalAmount,
            orderItems);

        var partnerTxId = RandomNumberGenerator.GetHexString(40, lowercase: true);
        var commerceSettings = await platformSettings.GetAsync<CommerceSettingsPayload>(
            PlatformSettingsCategoryKeys.Commerce, cancellationToken);
        var reservationMinutes = commerceSettings.ReservationTimeoutMinutes > 0
            ? commerceSettings.ReservationTimeoutMinutes
            : 30;
        var expiresOnUtc = DateTimeOffset.UtcNow.AddMinutes(reservationMinutes);
        var pendingPromotionsJson = evaluation.AppliedPromotions.Count > 0
            ? JsonSerializer.Serialize(evaluation.AppliedPromotions)
            : null;

        var paymentAttempt = PaymentAttempt.CreatePendingDot(
            order.Id,
            partnerTxId,
            wallet.ToOperatorId(),
            settings.ServiceId,
            chargeAmountResult.Value.Amount,
            chargeAmountResult.Value.Currency,
            expiresOnUtc,
            pendingPromotionsJson);

        commerceDbContext.Orders.Add(order);
        commerceDbContext.PaymentAttempts.Add(paymentAttempt);
        cart.Clear();

        // Persist the Pending order + attempt before ever calling out to DOT: if the process
        // crashes after the DOT call but before this save, we'd otherwise have a partner_txid DOT
        // knows about with no HAMBOX record to reconcile it against.
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        var tokenRequest = new DotAccessTokenRequest(
            partnerTxId,
            wallet.ToOperatorId(),
            chargeAmountResult.Value.Amount,
            settings.PublicRedirectUrl,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var tokenResult = await dotGateway.GetAccessTokenAsync(tokenRequest, cancellationToken);
        if (tokenResult.IsFailure || !tokenResult.Value.IsSuccess)
        {
            // Nothing was reserved or charged — leave the Pending order/attempt for the
            // reconciliation sweep to expire rather than inventing an extra state transition for
            // "DOT couldn't even issue a token." The customer sees a clean failure immediately.
            logger.LogWarning(
                "DOT GetAccessToken failed for partner_txid {PartnerTxId}: {Error}",
                partnerTxId,
                tokenResult.IsFailure ? tokenResult.Error.Description : tokenResult.Value.ResultDesc);

            return Result.Failure<DotCheckoutInitiationDto>(
                tokenResult.IsFailure ? tokenResult.Error : CommerceErrors.DotProviderUnavailable);
        }

        var landingPageUrl = dotGateway.BuildOtpLandingPageUrl(tokenResult.Value.Token!, tokenRequest);

        return Result.Success(new DotCheckoutInitiationDto(paymentAttempt.Id, order.Id, landingPageUrl, expiresOnUtc));
    }
}
