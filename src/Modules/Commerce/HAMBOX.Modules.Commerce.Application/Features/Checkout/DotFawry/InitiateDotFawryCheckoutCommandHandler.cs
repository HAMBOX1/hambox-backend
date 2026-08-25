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

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;

internal sealed class InitiateDotFawryCheckoutCommandHandler(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ICurrentUserService currentUserService,
    IInventoryEngine inventoryEngine,
    CartResponseBuilder cartResponseBuilder,
    CartLineValidator cartLineValidator,
    IMembershipAccessProvider membershipAccess,
    IDotFawryChargeAmountResolver chargeAmountResolver,
    IDotFawryPaymentGateway dotFawryGateway,
    IOptions<DotFawrySettings> dotFawryOptions,
    IPlatformSettingsProvider platformSettings,
    DotFawryPaymentVerificationService verificationService,
    ILogger<InitiateDotFawryCheckoutCommandHandler> logger)
    : IRequestHandler<InitiateDotFawryCheckoutCommand, Result<DotFawryCheckoutInitiationDto>>
{
    public async Task<Result<DotFawryCheckoutInitiationDto>> Handle(
        InitiateDotFawryCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<DotFawryCheckoutInitiationDto>(CommerceErrors.AuthenticationRequired);
        }

        var cart = await commerceDbContext.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == currentUserService.UserId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Failure<DotFawryCheckoutInitiationDto>(CommerceErrors.CartEmpty);
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
                return Result.Failure<DotFawryCheckoutInitiationDto>(CommerceErrors.MembershipPurchaseLimitExceeded(monthlyLimit));
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
            return Result.Failure<DotFawryCheckoutInitiationDto>(lineValidation.Error);
        }

        var (subtotal, discountAmount, taxAmount, totalAmount, evaluation) =
            await cartResponseBuilder.BuildOrderAmountsAsync(cart, request.Country, cancellationToken);

        if (evaluation.ValidationErrors.Count > 0)
        {
            return Result.Failure<DotFawryCheckoutInitiationDto>(
                CommerceErrors.InvalidCoupon(string.Join(' ', evaluation.ValidationErrors)));
        }

        var chargeAmountResult = await chargeAmountResolver.ResolveAsync(totalAmount, request.Country, cancellationToken);
        if (chargeAmountResult.IsFailure)
        {
            return Result.Failure<DotFawryCheckoutInitiationDto>(chargeAmountResult.Error);
        }

        var settings = dotFawryOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.PartnerId)
            || string.IsNullOrWhiteSpace(settings.ServiceId))
        {
            logger.LogError("DOT Fawry checkout attempted but DOT Fawry configuration is incomplete.");
            return Result.Failure<DotFawryCheckoutInitiationDto>(CommerceErrors.DotFawryGatewayMisconfigured);
        }

        // Guaranteed to parse — InitiateDotFawryCheckoutCommandValidator only lets a valid
        // DotFawryWalletOperator member name through before this handler ever runs.
        var wallet = Enum.Parse<DotFawryWalletOperator>(request.Wallet, ignoreCase: true);
        logger.LogInformation(
            "DOT Fawry checkout initiated: requested wallet {RequestedWallet}, resolved opId {OpId}",
            request.Wallet, wallet.ToOperatorId());

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
            "dot-fawry",
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

        var paymentAttempt = PaymentAttempt.CreatePendingDotFawry(
            order.Id,
            partnerTxId,
            wallet.ToOperatorId(),
            settings.ServiceId,
            chargeAmountResult.Value.Amount,
            chargeAmountResult.Value.Currency,
            MsisdnMasker.Mask(request.PhoneNumber) ?? "****",
            expiresOnUtc,
            pendingPromotionsJson);

        commerceDbContext.Orders.Add(order);
        commerceDbContext.PaymentAttempts.Add(paymentAttempt);

        // Cart is deliberately NOT cleared here: the customer hasn't paid yet (they still need to
        // complete payment in their wallet). Clearing it now would strand them with an empty cart if
        // the payment ultimately fails. DotFawryPaymentVerificationService clears it once payment is
        // actually confirmed — see its VerifyAndFinalizeAsync success path.

        // Persist the Pending order + attempt before ever calling out to DOT: if the process
        // crashes after the DOT call but before this save, we'd otherwise have a partnerTransId DOT
        // knows about with no HAMBOX record to reconcile it against.
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        var billingItems = orderItems
            .Select(i => new DotFawryOrderItem(
                i.sku ?? i.ProductId.ToString(),
                i.NameEn,
                i.UnitPrice,
                i.Quantity))
            .ToList();

        var chargeRequest = new DotFawryChargeRequest(
            partnerTxId,
            wallet.ToOperatorId(),
            request.PhoneNumber,
            chargeAmountResult.Value.Amount,
            request.Email,
            string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName,
            billingItems);

        var chargeResult = await dotFawryGateway.ChargeAsync(chargeRequest, cancellationToken);
        if (chargeResult.IsFailure)
        {
            // Nothing confirmed either way — leave the Pending order/attempt for the reconciliation
            // sweep to resolve (it may still find a real DOT transaction if the request reached DOT
            // but the response was lost) or expire. The customer sees a clean failure immediately.
            logger.LogWarning(
                "DOT Fawry ChargeAsync failed for partnerTransId {PartnerTxId}: {Error}",
                partnerTxId, chargeResult.Error.Description);

            return Result.Failure<DotFawryCheckoutInitiationDto>(chargeResult.Error);
        }

        var charge = chargeResult.Value;
        paymentAttempt.RecordProviderContext(charge.DotTransId, null, charge.ResultCode, charge.ResultDesc);
        paymentAttempt.RecordProviderReferenceCode(charge.FawryReferenceNumber);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        if (charge.IsProcessing)
        {
            // resultCode 1000 — the expected, confirmed-normal outcome for Fawry: awaiting the
            // customer's payment at Fawry. Never finalized here; only the notification webhook or
            // the reconciliation sweep, via DotFawryPaymentVerificationService, can move this
            // forward.
            return Result.Success(new DotFawryCheckoutInitiationDto(
                paymentAttempt.Id, order.Id, charge.FawryReferenceNumber, expiresOnUtc, wallet.ToString()));
        }

        // Any other resultCode — including "0" immediate success, which the docs say some operators
        // return synchronously — is never trusted by itself. Route through the same single
        // finalization path every other trigger uses, so the outcome (and every side effect:
        // inventory, promotions, referral points, confirmation email) is decided in exactly one
        // place. The frontend's status poll will already see the terminal state on its first tick.
        await verificationService.VerifyAndFinalizeAsync(paymentAttempt.Id, cancellationToken);

        return Result.Success(new DotFawryCheckoutInitiationDto(paymentAttempt.Id, order.Id, null, expiresOnUtc, wallet.ToString()));
    }
}
