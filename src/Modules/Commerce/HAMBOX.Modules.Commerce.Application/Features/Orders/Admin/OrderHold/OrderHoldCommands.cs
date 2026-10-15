using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.OrderHold;

public sealed record HoldAdminOrderCommand(Guid OrderId, string Reason) : IRequest<Result>;

public sealed record ReleaseAdminOrderHoldCommand(Guid OrderId, string? Note) : IRequest<Result>;

internal sealed class HoldAdminOrderCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<HoldAdminOrderCommand, Result>
{
    public async Task<Result> Handle(HoldAdminOrderCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure(CommerceErrors.HoldReasonRequired);
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(CommerceErrors.OrderNotFound);
        }

        try
        {
            order.PlaceOnHold(OrderHoldKind.AdminHold, request.Reason);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure(CommerceErrors.OrderCannotBeHeld);
        }

        var actorId = currentUser.UserId ?? "system";
        var actorName = currentUser.DisplayName ?? actorId;
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id, "OrderHeld", $"Order placed on hold: {order.HoldReason}", actorId, actorName,
            reason: order.HoldReason, oldValue: "NotOnHold", newValue: "AdminHold"));
        order.RecordAdminEdit(actorId, actorName);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ReleaseAdminOrderHoldCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser,
    IOperationalJobQueue jobQueue) : IRequestHandler<ReleaseAdminOrderHoldCommand, Result>
{
    public async Task<Result> Handle(ReleaseAdminOrderHoldCommand request, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(CommerceErrors.OrderNotFound);
        }

        if (!order.IsOnHold)
        {
            return Result.Failure(CommerceErrors.OrderNotOnHold);
        }

        var previousKind = order.HoldKind;
        var actorId = currentUser.UserId ?? "system";
        var actorName = currentUser.DisplayName ?? actorId;

        order.ReleaseHold(actorId);
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            previousKind == OrderHoldKind.ManualReview ? "ManualReviewApproved" : "OrderHoldReleased",
            string.IsNullOrWhiteSpace(request.Note) ? "Hold released." : $"Hold released: {request.Note.Trim()}",
            actorId,
            actorName,
            reason: string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            oldValue: previousKind.ToString(),
            newValue: "NotOnHold"));
        order.RecordAdminEdit(actorId, actorName);

        await db.SaveChangesAsync(cancellationToken);

        if (order.PaymentStatus == PaymentStatus.Paid && order.Status != OrderStatus.Completed)
        {
            // Same job every paid order goes through — stock, suppliers and referral hooks behave exactly as usual.
            await jobQueue.EnqueueAsync(
                OperationalJobTypes.ExecuteOrderFulfillment,
                JsonSerializer.Serialize(new { orderId = order.Id }),
                OperationalJobPriority.High,
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        return Result.Success();
    }
}
