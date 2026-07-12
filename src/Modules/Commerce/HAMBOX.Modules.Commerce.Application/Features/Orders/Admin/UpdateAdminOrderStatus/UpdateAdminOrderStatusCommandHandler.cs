using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
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
    private readonly ICurrentUserService _currentUserService;
    private readonly ISender _sender;

    public UpdateAdminOrderStatusCommandHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _sender = sender;
    }

    public async Task<Result<AdminOrderDetailDto>> Handle(
        UpdateAdminOrderStatusCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderNotFound);
        }

        if (!TryParseStatus(command.Request.Status, out var status))
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.InvalidOrderStatus(command.Request.Status));
        }

        try
        {
            order.SetAdminStatus(status);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderStatusTransitionFailed(ex.Message));
        }

        var actorId = _currentUserService.UserId ?? "system";
        var actorName = actorId;

        _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "StatusChanged",
            $"Order status changed to {command.Request.Status}.",
            actorId,
            actorName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetAdminOrderById.GetAdminOrderByIdQuery(order.Id), cancellationToken);
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
