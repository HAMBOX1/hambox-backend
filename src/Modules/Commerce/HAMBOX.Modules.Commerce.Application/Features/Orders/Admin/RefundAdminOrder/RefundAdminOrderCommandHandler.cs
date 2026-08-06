using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RefundAdminOrder;

public sealed record RefundAdminOrderCommand(Guid OrderId) : IRequest<Result<AdminOrderDetailDto>>;

internal sealed class RefundAdminOrderCommandHandler : IRequestHandler<RefundAdminOrderCommand, Result<AdminOrderDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICommerceTransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly OrderInventoryReleaseService _inventoryReleaseService;
    private readonly ISender _sender;

    public RefundAdminOrderCommandHandler(
        ICommerceDbContext dbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService,
        OrderInventoryReleaseService inventoryReleaseService,
        ISender sender)
    {
        _dbContext = dbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _inventoryReleaseService = inventoryReleaseService;
        _sender = sender;
    }

    public async Task<Result<AdminOrderDetailDto>> Handle(
        RefundAdminOrderCommand command,
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

                var wasAlreadyReleased = order.Status is OrderStatus.Cancelled or OrderStatus.Refunded;

                try
                {
                    order.Refund();
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidOperationException(CommerceErrors.OrderRefundNotSupported.Description);
                }

                var actorId = _currentUserService.UserId ?? "system";
                _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
                    order.Id,
                    "RefundIssued",
                    "Order was refunded by an administrator.",
                    actorId,
                    actorId));

                await _dbContext.SaveChangesAsync(ct);

                if (!wasAlreadyReleased)
                {
                    await _inventoryReleaseService.ReleaseAsync(order, actorId, ct);
                }
            }, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message == CommerceErrors.OrderNotFound.Description)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderNotFound);
        }
        catch (InvalidOperationException ex) when (ex.Message == CommerceErrors.OrderRefundNotSupported.Description)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderRefundNotSupported);
        }

        return await _sender.Send(new GetAdminOrderById.GetAdminOrderByIdQuery(order!.Id), cancellationToken);
    }
}
