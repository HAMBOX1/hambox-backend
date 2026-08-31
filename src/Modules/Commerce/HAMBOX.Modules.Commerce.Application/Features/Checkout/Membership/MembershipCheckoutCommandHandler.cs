using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Application.Idempotency;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Membership;

public sealed record MembershipCheckoutCommand(
    Guid PlanId,
    string Action,
    string Email,
    string Country,
    string PaymentMethod,
    string? CouponCode = null) : IRequest<Result<OrderDto>>, IIdempotentRequest<OrderDto>;

internal sealed class MembershipCheckoutCommandHandler
    : IRequestHandler<MembershipCheckoutCommand, Result<OrderDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICommerceTransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly MembershipOperationsService _membershipOperations;
    private readonly IEnumerable<IPaymentProvider> _paymentProviders;
    private readonly ICommunicationService _communicationService;
    private readonly ReferralLifecycleService _referralLifecycle;
    private readonly IPlatformSettingsProvider _platformSettings;
    private readonly ILogger<MembershipCheckoutCommandHandler> _logger;

    public MembershipCheckoutCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService,
        MembershipOperationsService membershipOperations,
        IEnumerable<IPaymentProvider> paymentProviders,
        ICommunicationService communicationService,
        ReferralLifecycleService referralLifecycle,
        IPlatformSettingsProvider platformSettings,
        ILogger<MembershipCheckoutCommandHandler> logger)
    {
        _commerceDbContext = commerceDbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _membershipOperations = membershipOperations;
        _paymentProviders = paymentProviders;
        _communicationService = communicationService;
        _referralLifecycle = referralLifecycle;
        _platformSettings = platformSettings;
        _logger = logger;
    }

    public async Task<Result<OrderDto>> Handle(
        MembershipCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is null)
        {
            return Result.Failure<OrderDto>(CommerceErrors.AuthenticationRequired);
        }

        if (!Enum.TryParse<MembershipCheckoutAction>(request.Action, ignoreCase: true, out var action))
        {
            return Result.Failure<OrderDto>(CommerceErrors.MembershipCheckoutActionInvalid);
        }

        var plan = await _commerceDbContext.MembershipPlans
            .Include(p => p.Benefits)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.Status == MembershipPlanStatus.Active, cancellationToken);

        if (plan is null)
        {
            return Result.Failure<OrderDto>(CommerceErrors.MembershipPlanNotFound);
        }

        var userId = _currentUserService.UserId!;
        var activeSubscription = await _commerceDbContext.MembershipSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.Active, cancellationToken);

        var validation = ValidateAction(action, plan, activeSubscription);
        if (validation.IsFailure)
        {
            return Result.Failure<OrderDto>(validation.Error);
        }

        var provider = _paymentProviders.FirstOrDefault(p => p.CanHandle(request.PaymentMethod));
        if (provider is null)
        {
            return Result.Failure<OrderDto>(CommerceErrors.PaymentMethodNotSupported);
        }

        MembershipSubscription? pendingSubscription = null;
        if (action == MembershipCheckoutAction.Subscribe)
        {
            await CancelStalePendingSubscriptionsAsync(userId, cancellationToken);
            pendingSubscription = MembershipSubscription.CreatePendingPayment(userId, plan.Id, autoRenew: true);
            _commerceDbContext.MembershipSubscriptions.Add(pendingSubscription);
            await _commerceDbContext.SaveChangesAsync(cancellationToken);
        }

        var subscriptionId = action == MembershipCheckoutAction.Subscribe
            ? pendingSubscription!.Id
            : activeSubscription!.Id;

        var totalAmount = plan.Price;
        Order? createdOrder = null;

        var commerceSettings = await _platformSettings.GetAsync<CommerceSettingsPayload>(
            PlatformSettingsCategoryKeys.Commerce, cancellationToken);

        try
        {
            PaymentProviderResult? paymentResult = null;

            await _transactionService.ExecuteAsync(async ct =>
            {
                paymentResult = await provider.ProcessAsync(
                    new PaymentProviderRequest(
                        totalAmount,
                        "USD",
                        request.PaymentMethod,
                        request.Email,
                        userId),
                    ct);

                if (!paymentResult.IsSuccess)
                {
                    throw new InvalidOperationException(
                        paymentResult.FailureReason ?? CommerceErrors.PaymentFailed.Description);
                }

                var orderNumber = OrderNumberGenerator.Generate(commerceSettings.InvoicePrefix);
                var order = Order.CreateForMembership(
                    userId,
                    orderNumber,
                    request.Email,
                    request.Country,
                    request.PaymentMethod,
                    totalAmount,
                    plan.Id,
                    plan.Name,
                    action,
                    subscriptionId);

                order.RecordPayment(paymentResult!.Provider, paymentResult.TransactionId!);
                OrderPaymentCallbackRecorder.Record(_commerceDbContext, order.Id, paymentResult);
                order.Complete();

                var subscription = action == MembershipCheckoutAction.Subscribe
                    ? pendingSubscription!
                    : activeSubscription!;

                await _membershipOperations.FulfillAfterPaymentAsync(
                    subscription,
                    plan,
                    action,
                    order.Id,
                    paymentResult.TransactionId,
                    userId,
                    ct);

                order.LinkMembershipSubscription(subscription.Id);

                // Membership checkout never evaluates cart promotions, so there's no referee discount
                // to race here — this only exists so a referred user's first purchase can be a
                // membership plan, not just a product order. Kept inside the same transaction as the
                // other completion paths for consistency, not because it needs the atomicity here.
                await _referralLifecycle.ProcessOrderCompletedAsync(order, ct);

                _logger.LogInformation(
                    "Membership checkout completed for {UserId}: plan {PlanId}, action {Action}, order {OrderNumber}",
                    userId,
                    plan.Id,
                    action,
                    order.OrderNumber);

                _commerceDbContext.Orders.Add(order);
                await _commerceDbContext.SaveChangesAsync(ct);
                createdOrder = order;
            }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            if (pendingSubscription is not null)
            {
                pendingSubscription.CancelPendingPayment();
                await _commerceDbContext.SaveChangesAsync(cancellationToken);
            }

            if (ex.Message.Contains("payment", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<OrderDto>(new Error(CommerceErrors.PaymentFailed.Code, ex.Message));
            }

            return Result.Failure<OrderDto>(new Error("MembershipCheckout.Failed", ex.Message));
        }

        var actionLabel = action switch
        {
            MembershipCheckoutAction.Subscribe => "activated",
            MembershipCheckoutAction.Renew => "renewed",
            MembershipCheckoutAction.Upgrade => "upgraded",
            MembershipCheckoutAction.Downgrade => "changed",
            _ => "updated",
        };

        await _communicationService.SendAsync(new CommunicationRequest(
            UserId: userId,
            TemplateKey: "MembershipConfirmation",
            Category: CommunicationCategory.Membership,
            Variables: new Dictionary<string, string>
            {
                ["PlanName"] = plan.Name,
                ["ActionLabel"] = actionLabel,
                ["OrderNumber"] = createdOrder!.OrderNumber,
            },
            RelatedEntityType: "Order",
            RelatedEntityId: createdOrder.Id.ToString(),
            ActionUrl: "/account/membership"), cancellationToken);

        return Result.Success(CommerceMapper.ToOrderDto(createdOrder));
    }

    private static Result ValidateAction(
        MembershipCheckoutAction action,
        MembershipPlan plan,
        MembershipSubscription? activeSubscription) =>
        action switch
        {
            MembershipCheckoutAction.Subscribe when activeSubscription is not null =>
                Result.Failure(CommerceErrors.MembershipSubscriptionAlreadyActive),
            MembershipCheckoutAction.Subscribe => Result.Success(),
            MembershipCheckoutAction.Renew when activeSubscription is null =>
                Result.Failure(CommerceErrors.MembershipSubscriptionNotFound),
            MembershipCheckoutAction.Upgrade or MembershipCheckoutAction.Downgrade
                when activeSubscription is null =>
                Result.Failure(CommerceErrors.MembershipSubscriptionNotFound),
            MembershipCheckoutAction.Upgrade or MembershipCheckoutAction.Downgrade
                when activeSubscription!.PlanId == plan.Id =>
                Result.Failure(CommerceErrors.MembershipPlanUnchanged),
            MembershipCheckoutAction.Renew => Result.Success(),
            MembershipCheckoutAction.Upgrade or MembershipCheckoutAction.Downgrade => Result.Success(),
            _ => Result.Failure(CommerceErrors.MembershipCheckoutActionInvalid),
        };

    private async Task CancelStalePendingSubscriptionsAsync(string userId, CancellationToken cancellationToken)
    {
        var stale = await _commerceDbContext.MembershipSubscriptions
            .Where(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.PendingPayment)
            .ToListAsync(cancellationToken);

        foreach (var subscription in stale)
        {
            subscription.CancelPendingPayment();
        }
    }
}
