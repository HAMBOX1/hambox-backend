using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.UpdateAdminOrderStatus;

public sealed record UpdateAdminOrderStatusCommand(Guid OrderId, UpdateAdminOrderStatusRequest Request)
    : IRequest<Result<AdminOrderDetailDto>>;

internal sealed class UpdateAdminOrderStatusCommandHandler
    : IRequestHandler<UpdateAdminOrderStatusCommand, Result<AdminOrderDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICommerceTransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly OrderInventoryReleaseService _inventoryReleaseService;
    private readonly ReferralLifecycleService _referralLifecycle;
    private readonly ISender _sender;

    public UpdateAdminOrderStatusCommandHandler(
        ICommerceDbContext dbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService,
        OrderInventoryReleaseService inventoryReleaseService,
        ReferralLifecycleService referralLifecycle,
        ISender sender)
    {
        _dbContext = dbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _inventoryReleaseService = inventoryReleaseService;
        _referralLifecycle = referralLifecycle;
        _sender = sender;
    }

    public async Task<Result<AdminOrderDetailDto>> Handle(
        UpdateAdminOrderStatusCommand command,
        CancellationToken cancellationToken)
    {
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

                if (!TryParseStatus(command.Request.Status, out var status))
                {
                    throw new InvalidOperationException(CommerceErrors.InvalidOrderStatus(command.Request.Status).Description);
                }

                var wasAlreadyReleased = order.Status is OrderStatus.Cancelled or OrderStatus.Refunded;

                order.SetAdminStatus(status);

                var actorId = _currentUserService.UserId ?? "system";
                var actorName = _currentUserService.DisplayName ?? actorId;
                _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
                    order.Id,
                    "StatusChanged",
                    $"Order status changed to {command.Request.Status}.",
                    actorId,
                    actorName));
                order.RecordAdminEdit(actorId, actorName);

                await _dbContext.SaveChangesAsync(ct);

                if (!wasAlreadyReleased && status is OrderStatus.Cancelled or OrderStatus.Refunded)
                {
                    await _inventoryReleaseService.ReleaseAsync(order, actorId, ct);
                    await _referralLifecycle.ReverseForOrderAsync(order, ct);
                }

                if (status == OrderStatus.Completed)
                {
                    // order.SetAdminStatus(Completed) above already threw if the order was Completed
                    // before this call (Order.Complete()'s own guard), so reaching here always means
                    // this is a genuine, first-time completion — safe to process unconditionally.
                    await _referralLifecycle.ProcessOrderCompletedAsync(order, ct);
                }
            }, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message == CommerceErrors.OrderNotFound.Description)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderNotFound);
        }
        catch (InvalidOperationException ex)
            when (ex.Message == CommerceErrors.InvalidOrderStatus(command.Request.Status).Description)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.InvalidOrderStatus(command.Request.Status));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderStatusTransitionFailed(ex.Message));
        }

        return await _sender.Send(new GetAdminOrderById.GetAdminOrderByIdQuery(order!.Id), cancellationToken);
    }

    private static bool TryParseStatus(string status, out OrderStatus parsed)
    {
        if (Enum.TryParse(status, ignoreCase: true, out parsed))
        {
            return true;
        }

        parsed = status.ToLowerInvariant() switch
        {
            "processing" => OrderStatus.Processing,
            "completed" => OrderStatus.Completed,
            "cancelled" => OrderStatus.Cancelled,
            "refunded" => OrderStatus.Refunded,
            "failed" => OrderStatus.Failed,
            "pending" => OrderStatus.Pending,
            _ => default,
        };

        return status.ToLowerInvariant() is "processing" or "completed" or "cancelled" or "refunded" or "failed" or "pending";
    }
}
