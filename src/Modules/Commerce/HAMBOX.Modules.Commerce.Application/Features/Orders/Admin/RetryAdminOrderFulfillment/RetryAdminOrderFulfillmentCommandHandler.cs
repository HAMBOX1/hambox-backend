using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RetryAdminOrderFulfillment;

public sealed record RetryAdminOrderFulfillmentCommand(Guid OrderId)
    : IRequest<Result<RetryAdminOrderFulfillmentResultDto>>;

internal sealed class RetryAdminOrderFulfillmentCommandHandler
    : IRequestHandler<RetryAdminOrderFulfillmentCommand, Result<RetryAdminOrderFulfillmentResultDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICommerceTransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly OrderFulfillmentService _fulfillmentService;
    private readonly IOperationalJobQueue _jobQueue;
    private readonly ICommunicationService _communicationService;
    private readonly ReferralLifecycleService _referralLifecycle;

    public RetryAdminOrderFulfillmentCommandHandler(
        ICommerceDbContext dbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService,
        OrderFulfillmentService fulfillmentService,
        IOperationalJobQueue jobQueue,
        ICommunicationService communicationService,
        ReferralLifecycleService referralLifecycle)
    {
        _dbContext = dbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _fulfillmentService = fulfillmentService;
        _jobQueue = jobQueue;
        _communicationService = communicationService;
        _referralLifecycle = referralLifecycle;
    }

    public async Task<Result<RetryAdminOrderFulfillmentResultDto>> Handle(
        RetryAdminOrderFulfillmentCommand command,
        CancellationToken cancellationToken)
    {
        OrderFulfillmentResult? result = null;
        Order? order = null;

        try
        {
            await _transactionService.ExecuteAsync(async ct =>
            {
                order = await _dbContext.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

                if (order is null)
                {
                    throw new InvalidOperationException(CommerceErrors.OrderNotFound.Description);
                }

                result = await _fulfillmentService.FulfillMissingAsync(order, ct);

                if (result.CodesDelivered == 0)
                {
                    throw new InvalidOperationException(CommerceErrors.OrderFulfillmentNothingToRetry.Description);
                }

                var actorId = _currentUserService.UserId ?? "system";
                var actorName = _currentUserService.DisplayName ?? actorId;
                _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
                    order.Id,
                    "FulfillmentRetried",
                    $"Fulfillment retry delivered {result.CodesDelivered} code(s).",
                    actorId,
                    actorName));
                order.RecordAdminEdit(actorId, actorName);

                await _dbContext.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message == CommerceErrors.OrderNotFound.Description)
            {
                return Result.Failure<RetryAdminOrderFulfillmentResultDto>(CommerceErrors.OrderNotFound);
            }

            if (ex.Message == CommerceErrors.OrderFulfillmentNothingToRetry.Description)
            {
                // Manual stock had nothing to offer — try an automated supplier for the shortfall
                // before falling back to the generic retry job. Best-effort: this call never throws
                // (it catches its own exceptions internally), and its outcome does not change the
                // response below — the existing retry-job safety net still fires unconditionally, so
                // an automated-supplier failure here is never worse than today's behavior.
                if (order is not null)
                {
                    await _fulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, cancellationToken);
                }

                await _jobQueue.EnqueueAsync(
                    OperationalJobTypes.RetryOrderFulfillment,
                    JsonSerializer.Serialize(new { orderId = command.OrderId }),
                    OperationalJobPriority.High,
                    relatedEntityType: "Order",
                    relatedEntityId: command.OrderId.ToString(),
                    cancellationToken: cancellationToken);

                return Result.Failure<RetryAdminOrderFulfillmentResultDto>(CommerceErrors.OrderFulfillmentNothingToRetry);
            }

            return Result.Failure<RetryAdminOrderFulfillmentResultDto>(
                CommerceErrors.OrderFulfillmentFailed);
        }

        // Manual retry delivered something but possibly not everything (e.g. one product line still
        // short) — top up via an automated supplier for whatever remains, strictly after the
        // transaction above has committed. See QueueAutomatedSupplierFulfillmentAsync's remarks.
        await _fulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order!, cancellationToken);

        if (result!.OrderCompleted)
        {
            await _referralLifecycle.ProcessOrderCompletedAsync(order!, cancellationToken);

            await _communicationService.SendAsync(new CommunicationRequest(
                UserId: order!.UserId,
                TemplateKey: "OrderConfirmation",
                Category: CommunicationCategory.Order,
                Variables: new Dictionary<string, string>
                {
                    ["OrderNumber"] = order.OrderNumber,
                    ["Total"] = order.TotalAmount.ToString("0.00"),
                },
                RelatedEntityType: "Order",
                RelatedEntityId: order.Id.ToString(),
                ActionUrl: $"/account/library?orderId={order.Id}"), cancellationToken);
        }

        return Result.Success(new RetryAdminOrderFulfillmentResultDto(
            result.CodesDelivered,
            result.OrderCompleted));
    }
}
