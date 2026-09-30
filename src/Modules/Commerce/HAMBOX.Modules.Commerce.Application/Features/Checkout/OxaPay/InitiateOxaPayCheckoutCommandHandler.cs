using System.Security.Cryptography;
using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Membership;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

internal sealed class InitiateOxaPayCheckoutCommandHandler(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ILegalDbContext legalDbContext,
    ICurrentUserService currentUserService,
    IInventoryEngine inventoryEngine,
    CartResponseBuilder cartResponseBuilder,
    CartLineValidator cartLineValidator,
    IMembershipAccessProvider membershipAccess,
    IOxaPayPaymentGateway oxapayGateway,
    IPaymentGatewayConfigurationProvider settingsProvider,
    IPlatformSettingsProvider platformSettings,
    ILogger<InitiateOxaPayCheckoutCommandHandler> logger)
    : IRequestHandler<InitiateOxaPayCheckoutCommand, Result<OxaPayCheckoutInitiationDto>>
{
    public async Task<Result<OxaPayCheckoutInitiationDto>> Handle(
        InitiateOxaPayCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<OxaPayCheckoutInitiationDto>(CommerceErrors.AuthenticationRequired);
        }

        var cart = await commerceDbContext.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == currentUserService.UserId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Failure<OxaPayCheckoutInitiationDto>(CommerceErrors.CartEmpty);
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
                return Result.Failure<OxaPayCheckoutInitiationDto>(CommerceErrors.MembershipPurchaseLimitExceeded(monthlyLimit));
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
            return Result.Failure<OxaPayCheckoutInitiationDto>(lineValidation.Error);
        }

        CartLineValidator.ApplyResolvedPricing(cart, lineValidation.Value);
        var resolvedPricingByLine = lineValidation.Value.Lines.ToDictionary(l => (l.ProductId, l.ProductVariantId));

        var (subtotal, discountAmount, taxAmount, totalAmount, evaluation) =
            await cartResponseBuilder.BuildOrderAmountsAsync(cart, request.Country, cancellationToken, "oxapay");

        if (evaluation.ValidationErrors.Count > 0)
        {
            return Result.Failure<OxaPayCheckoutInitiationDto>(
                CommerceErrors.InvalidCoupon(string.Join(' ', evaluation.ValidationErrors)));
        }

        // Unlike Cryptomus, OxaPay's API key only ever lives in the admin-managed DB row (no
        // appsettings/env-var fallback), so the fail-fast check must read the DB-resolved effective
        // settings here, not IOptions<OxaPaySettings> directly.
        var settings = await settingsProvider.GetOxaPaySettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.MerchantApiKey)
            || string.IsNullOrWhiteSpace(settings.PublicWebhookUrl))
        {
            logger.LogError("OxaPay checkout attempted but OxaPay configuration is incomplete.");
            return Result.Failure<OxaPayCheckoutInitiationDto>(CommerceErrors.OxaPayGatewayMisconfigured);
        }

        var commerceSettings = await platformSettings.GetAsync<CommerceSettingsPayload>(
            PlatformSettingsCategoryKeys.Commerce, cancellationToken);
        var orderNumber = OrderNumberGenerator.Generate(commerceSettings.InvoicePrefix);
        var orderItems = cart.Items
            .Select(item =>
            {
                var variantId = item.ProductVariantId;
                string? sku = null;
                if (variantId is not null && variants.TryGetValue(variantId.Value, out var variant))
                {
                    sku = variant.Sku;
                }

                var resolved = resolvedPricingByLine[(item.ProductId, variantId)];

                return (
                    item.ProductId,
                    products[item.ProductId].NameEn,
                    item.Quantity,
                    item.UnitPrice,
                    variantId,
                    sku,
                    resolved.SelectedSupplierId,
                    resolved.SelectedSupplierProductMappingId,
                    resolved.SupplierBuyingPriceAtOrderTime,
                    resolved.MarginPercentAppliedAtOrderTime);
            })
            .ToList();

        var order = Order.Create(
            currentUserService.UserId!,
            orderNumber,
            request.Email,
            request.Country,
            "oxapay",
            subtotal,
            discountAmount,
            taxAmount,
            totalAmount,
            orderItems);

        var partnerTxId = RandomNumberGenerator.GetHexString(40, lowercase: true);
        var reservationMinutes = commerceSettings.ReservationTimeoutMinutes > 0
            ? commerceSettings.ReservationTimeoutMinutes
            : 30;
        var expiresOnUtc = DateTimeOffset.UtcNow.AddMinutes(reservationMinutes);
        var pendingPromotionsJson = evaluation.AppliedPromotions.Count > 0
            ? JsonSerializer.Serialize(evaluation.AppliedPromotions)
            : null;

        var paymentAttempt = PaymentAttempt.CreatePendingOxaPay(
            order.Id,
            partnerTxId,
            totalAmount,
            "USD",
            expiresOnUtc,
            pendingPromotionsJson);

        commerceDbContext.Orders.Add(order);
        commerceDbContext.PaymentAttempts.Add(paymentAttempt);

        // Cart is deliberately NOT cleared here — same reasoning as InitiateCryptomusCheckoutCommandHandler:
        // the customer hasn't paid yet. OxaPayPaymentVerificationService clears it once payment is
        // actually confirmed.

        // Persist the Pending order + attempt before ever calling out to OxaPay: if the process
        // crashes after the OxaPay call but before this save, we'd otherwise have a track_id OxaPay
        // knows about with no HAMBOX record to reconcile it against.
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        await LegalAcceptanceRecorder.RecordAsync(
            legalDbContext,
            currentUserService.UserId!,
            request.IpAddress,
            request.UserAgent,
            request.Language,
            order.Id,
            cancellationToken);
        await legalDbContext.SaveChangesAsync(cancellationToken);

        var returnUrl = $"{settings.FrontendResultUrl.TrimEnd('/')}?paymentAttemptId={paymentAttempt.Id}";
        var invoiceResult = await oxapayGateway.CreateInvoiceAsync(
            new OxaPayCreateInvoiceRequest(partnerTxId, totalAmount, returnUrl), cancellationToken);

        if (invoiceResult.IsFailure || string.IsNullOrWhiteSpace(invoiceResult.Value.PaymentUrl))
        {
            // Nothing was charged — leave the Pending order/attempt for the reconciliation sweep to
            // expire rather than inventing an extra state transition, same as Cryptomus's equivalent path.
            logger.LogWarning(
                "OxaPay CreateInvoice failed for order_id {OrderId}: {Error}",
                partnerTxId,
                invoiceResult.IsFailure ? invoiceResult.Error.Description : "no payment URL returned");

            return Result.Failure<OxaPayCheckoutInitiationDto>(
                invoiceResult.IsFailure ? invoiceResult.Error : CommerceErrors.OxaPayProviderUnavailable);
        }

        paymentAttempt.RecordProviderContext(invoiceResult.Value.TrackId, null, null, null);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new OxaPayCheckoutInitiationDto(
            paymentAttempt.Id, order.Id, invoiceResult.Value.PaymentUrl!, expiresOnUtc));
    }
}
