using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
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

    public RetryAdminOrderFulfillmentCommandHandler(
        ICommerceDbContext dbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService,
        OrderFulfillmentService fulfillmentService,
        IOperationalJobQueue jobQueue)
    {
        _dbContext = dbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _fulfillmentService = fulfillmentService;
        _jobQueue = jobQueue;
    }

    public async Task<Result<RetryAdminOrderFulfillmentResultDto>> Handle(
        RetryAdminOrderFulfillmentCommand command,
        CancellationToken cancellationToken)
    {
        OrderFulfillmentResult? result = null;

        try
        {
            await _transactionService.ExecuteAsync(async ct =>
            {
                var order = await _dbContext.Orders
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
                _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
                    order.Id,
                    "FulfillmentRetried",
                    $"Fulfillment retry delivered {result.CodesDelivered} code(s).",
                    actorId,
                    actorId));

                if (result.OrderCompleted)
                {
                    _dbContext.UserNotifications.Add(Domain.Account.UserNotification.Create(
                        order.UserId,
                        "Order completed",
                        $"Your order {order.OrderNumber} is complete. License keys are ready in My Library.",
                        "Order",
                        $"/account/library?orderId={order.Id}"));
                }

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

        return Result.Success(new RetryAdminOrderFulfillmentResultDto(
            result!.CodesDelivered,
            result.OrderCompleted));
    }
}
