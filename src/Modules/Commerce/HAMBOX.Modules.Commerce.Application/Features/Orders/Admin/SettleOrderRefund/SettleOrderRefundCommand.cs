using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.SettleOrderRefund;

/// <summary>An admin confirms the money for a refunded / cancelled order was returned (gateway reference required).</summary>
public sealed record SettleOrderRefundCommand(Guid OrderId, string GatewayReference, string? Note) : IRequest<Result>;

internal sealed class SettleOrderRefundCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<SettleOrderRefundCommand, Result>
{
    public async Task<Result> Handle(SettleOrderRefundCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GatewayReference))
        {
            return Result.Failure(CommerceErrors.RefundReferenceRequired);
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(CommerceErrors.OrderNotFound);
        }

        if (order.RefundSettlement == RefundSettlementStatus.None)
        {
            return Result.Failure(CommerceErrors.OrderHasNoRefundToSettle);
        }

        var actorId = currentUser.UserId ?? "system";
        var actorName = currentUser.DisplayName ?? actorId;

        order.SettleRefund(request.GatewayReference, request.Note, actorId);
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "RefundSettled",
            $"Refund returned to the customer via the gateway (reference {order.RefundGatewayReference}).",
            actorId,
            actorName));
        order.RecordAdminEdit(actorId, actorName);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
