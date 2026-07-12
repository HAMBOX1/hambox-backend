using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RefundAdminOrder;

public sealed record RefundAdminOrderCommand(Guid OrderId) : IRequest<Result<AdminOrderDetailDto>>;

internal sealed class RefundAdminOrderCommandHandler : IRequestHandler<RefundAdminOrderCommand, Result<AdminOrderDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISender _sender;

    public RefundAdminOrderCommandHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _sender = sender;
    }

    public async Task<Result<AdminOrderDetailDto>> Handle(
        RefundAdminOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderNotFound);
        }

        try
        {
            order.Refund();
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderRefundNotSupported);
        }

        var actorId = _currentUserService.UserId ?? "system";
        _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "RefundIssued",
            "Order was refunded by an administrator.",
            actorId,
            actorId));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetAdminOrderById.GetAdminOrderByIdQuery(order.Id), cancellationToken);
    }
}
