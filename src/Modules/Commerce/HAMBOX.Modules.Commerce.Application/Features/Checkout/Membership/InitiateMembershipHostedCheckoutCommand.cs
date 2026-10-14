using HAMBOX.Modules.Commerce.Application.Options;
using System.Security.Cryptography;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Membership;

/// <summary>
/// Buying a membership through a hosted gateway (audit R32). In production there is no synchronous card processor,
/// so the plan is paid exactly like a cart order: a Pending membership order + payment attempt, a redirect to the
/// gateway's hosted page, and — once the gateway confirms — the verification service activates the subscription.
/// <paramref name="Gateway"/> is "cryptomus", "oxapay" or "dot" (DOT OTP wallets — <paramref name="Wallet"/> then names OrangeCash / VodafoneCash).
/// </summary>
public sealed record InitiateMembershipHostedCheckoutCommand(
    Guid PlanId,
    string Action,
    string Gateway,
    string Email,
    string Country,
    string IpAddress,
    string UserAgent,
    string Language,
    string? Wallet = null) : IRequest<Result<MembershipHostedCheckoutInitiationDto>>;

public sealed record MembershipHostedCheckoutInitiationDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string PaymentUrl,
    DateTimeOffset ExpiresOnUtc);

internal sealed class InitiateMembershipHostedCheckoutCommandHandler(
    ICommerceDbContext db,
    ILegalDbContext legalDb,
    ICurrentUserService currentUser,
    ICryptomusPaymentGateway cryptomusGateway,
    IOxaPayPaymentGateway oxapayGateway,
    IDotPaymentGateway dotGateway,
    IDotPricePointResolver dotPricePointResolver,
    IPaymentGatewayConfigurationProvider gatewaySettings,
    IPlatformSettingsProvider platformSettings,
    ILogger<InitiateMembershipHostedCheckoutCommandHandler> logger)
    : IRequestHandler<InitiateMembershipHostedCheckoutCommand, Result<MembershipHostedCheckoutInitiationDto>>
{
    public async Task<Result<MembershipHostedCheckoutInitiationDto>> Handle(
        InitiateMembershipHostedCheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.AuthenticationRequired);
        }

        var userId = currentUser.UserId;
        var gateway = request.Gateway.Trim().ToLowerInvariant();
        if (gateway is not ("cryptomus" or "oxapay" or "dot"))
        {
            return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.PaymentMethodNotSupported);
        }

        if (!Enum.TryParse<MembershipCheckoutAction>(request.Action, ignoreCase: true, out var action))
        {
            return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.MembershipCheckoutActionInvalid);
        }

        var plan = await db.MembershipPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.Status == MembershipPlanStatus.Active, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.MembershipPlanNotFound);
        }

        var activeSubscription = await db.MembershipSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.Active, cancellationToken);

        var validation = MembershipCheckoutCommandHandler.ValidateAction(action, plan, activeSubscription);
        if (validation.IsFailure)
        {
            return Result.Failure<MembershipHostedCheckoutInitiationDto>(validation.Error);
        }

        if (!await gatewaySettings.IsEnabledAsync(gateway, cancellationToken))
        {
            return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.PaymentMethodNotSupported);
        }

        string frontendResultUrl;
        HAMBOX.Modules.Commerce.Application.Options.DotSettings? dotSettings = null;

        HAMBOX.Modules.Commerce.Application.Options.DotWalletOperator wallet = default;
        if (gateway == "dot"
            && !Enum.TryParse(request.Wallet, ignoreCase: true, out wallet))
        {
            return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.PaymentMethodNotSupported);
        }

        if (gateway == "cryptomus")
        {
            var settings = await gatewaySettings.GetCryptomusSettingsAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(settings.MerchantId) || string.IsNullOrWhiteSpace(settings.ApiKey)
                || string.IsNullOrWhiteSpace(settings.PublicWebhookUrl))
            {
                logger.LogError("Cryptomus membership checkout attempted but Cryptomus configuration is incomplete.");
                return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.CryptomusGatewayMisconfigured);
            }

            frontendResultUrl = settings.FrontendResultUrl;
        }
        else if (gateway == "dot")
        {
            var settings = await gatewaySettings.GetDotSettingsAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(settings.PartnerId) || string.IsNullOrWhiteSpace(settings.ServiceId)
                || string.IsNullOrWhiteSpace(settings.PublicRedirectUrl))
            {
                logger.LogError("DOT membership checkout attempted but DOT configuration is incomplete.");
                return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.DotGatewayMisconfigured);
            }

            dotSettings = settings;
            frontendResultUrl = settings.FrontendResultUrl;
        }
        else
        {
            var settings = await gatewaySettings.GetOxaPaySettingsAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(settings.MerchantApiKey) || string.IsNullOrWhiteSpace(settings.PublicWebhookUrl))
            {
                logger.LogError("OxaPay membership checkout attempted but OxaPay configuration is incomplete.");
                return Result.Failure<MembershipHostedCheckoutInitiationDto>(CommerceErrors.OxaPayGatewayMisconfigured);
            }

            frontendResultUrl = settings.FrontendResultUrl;
        }

        MembershipSubscription? pendingSubscription = null;
        if (action == MembershipCheckoutAction.Subscribe)
        {
            var stale = await db.MembershipSubscriptions
                .Where(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.PendingPayment)
                .ToListAsync(cancellationToken);
            foreach (var subscription in stale)
            {
                subscription.CancelPendingPayment();
            }

            pendingSubscription = MembershipSubscription.CreatePendingPayment(userId, plan.Id, autoRenew: true);
            db.MembershipSubscriptions.Add(pendingSubscription);
        }

        var subscriptionId = action == MembershipCheckoutAction.Subscribe ? pendingSubscription!.Id : activeSubscription!.Id;
        var totalAmount = plan.Price;

        var commerceSettings = await platformSettings.GetAsync<CommerceSettingsPayload>(
            PlatformSettingsCategoryKeys.Commerce, cancellationToken);
        var reservationMinutes = commerceSettings.ReservationTimeoutMinutes > 0 ? commerceSettings.ReservationTimeoutMinutes : 30;
        var expiresOnUtc = DateTimeOffset.UtcNow.AddMinutes(reservationMinutes);

        var order = Order.CreateForMembership(
            userId,
            OrderNumberGenerator.Generate(commerceSettings.InvoicePrefix),
            request.Email,
            request.Country,
            gateway switch { "cryptomus" => "crypto", "dot" => "dot", _ => "oxapay" },
            totalAmount,
            plan.Id,
            plan.Name,
            action,
            subscriptionId);

        var partnerTxId = RandomNumberGenerator.GetHexString(40, lowercase: true);
        DotChargeAmount? dotCharge = null;
        if (gateway == "dot")
        {
            var charge = await dotPricePointResolver.ResolveAsync(totalAmount, request.Country, cancellationToken);
            if (charge.IsFailure)
            {
                pendingSubscription?.CancelPendingPayment();
                await db.SaveChangesAsync(cancellationToken);
                return Result.Failure<MembershipHostedCheckoutInitiationDto>(charge.Error);
            }

            dotCharge = charge.Value;
        }

        var attempt = gateway switch
        {
            "cryptomus" => PaymentAttempt.CreatePendingCryptomus(order.Id, partnerTxId, totalAmount, "USD", expiresOnUtc, null),
            "dot" => PaymentAttempt.CreatePendingDot(
                order.Id, partnerTxId, wallet.ToOperatorId(), dotSettings!.ServiceId,
                dotCharge!.Amount, dotCharge.Currency, expiresOnUtc, null),
            _ => PaymentAttempt.CreatePendingOxaPay(order.Id, partnerTxId, totalAmount, "USD", expiresOnUtc, null),
        };

        db.Orders.Add(order);
        db.PaymentAttempts.Add(attempt);

        // Persist the pending order + attempt before ever calling the gateway, same reasoning as the cart flows:
        // a crash after the call must not leave a gateway invoice with no HAMBOX record to reconcile it against.
        await db.SaveChangesAsync(cancellationToken);

        await LegalAcceptanceRecorder.RecordAsync(
            legalDb, userId, request.IpAddress, request.UserAgent, request.Language, order.Id, cancellationToken);
        await legalDb.SaveChangesAsync(cancellationToken);

        var returnUrl = $"{frontendResultUrl.TrimEnd('/')}?paymentAttemptId={attempt.Id}";

        string? paymentUrl;
        string? providerReference;
        if (gateway == "cryptomus")
        {
            var invoice = await cryptomusGateway.CreateInvoiceAsync(
                new CryptomusCreateInvoiceRequest(partnerTxId, totalAmount, returnUrl), cancellationToken);
            paymentUrl = invoice.IsSuccess ? invoice.Value.Url : null;
            providerReference = invoice.IsSuccess ? invoice.Value.Uuid : null;
        }
        else if (gateway == "dot")
        {
            var tokenRequest = new DotAccessTokenRequest(
                partnerTxId,
                wallet.ToOperatorId(),
                dotCharge!.Amount,
                dotSettings!.PublicRedirectUrl,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var token = await dotGateway.GetAccessTokenAsync(tokenRequest, cancellationToken);
            if (token.IsSuccess && token.Value.IsSuccess && !string.IsNullOrWhiteSpace(token.Value.Token))
            {
                paymentUrl = await dotGateway.BuildOtpLandingPageUrlAsync(token.Value.Token!, tokenRequest, cancellationToken);
            }
            else
            {
                paymentUrl = null;
            }

            providerReference = null;
        }
        else
        {
            var invoice = await oxapayGateway.CreateInvoiceAsync(
                new OxaPayCreateInvoiceRequest(partnerTxId, totalAmount, returnUrl), cancellationToken);
            paymentUrl = invoice.IsSuccess ? invoice.Value.PaymentUrl : null;
            providerReference = invoice.IsSuccess ? invoice.Value.TrackId : null;
        }

        if (string.IsNullOrWhiteSpace(paymentUrl))
        {
            logger.LogWarning("Membership invoice could not be created for order {OrderId} via {Gateway}.", order.Id, gateway);
            pendingSubscription?.CancelPendingPayment();
            order.MarkFailed();
            await db.SaveChangesAsync(cancellationToken);

            return Result.Failure<MembershipHostedCheckoutInitiationDto>(gateway switch
            {
                "cryptomus" => CommerceErrors.CryptomusProviderUnavailable,
                "dot" => CommerceErrors.DotProviderUnavailable,
                _ => CommerceErrors.OxaPayProviderUnavailable,
            });
        }

        if (!string.IsNullOrWhiteSpace(providerReference))
        {
            attempt.RecordProviderContext(providerReference, null, null, null);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new MembershipHostedCheckoutInitiationDto(attempt.Id, order.Id, paymentUrl, expiresOnUtc));
    }
}
