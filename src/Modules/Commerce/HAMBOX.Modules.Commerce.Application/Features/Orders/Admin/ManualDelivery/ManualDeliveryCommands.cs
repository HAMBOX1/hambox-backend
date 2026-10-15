using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Support;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.ManualDelivery;

/// <summary>
/// Closes a paid order that was delivered outside the system (over chat, by e-mail, by hand). A reason AND evidence
/// are mandatory, and both are written to the audit trail with who / when / from where.
/// </summary>
public sealed record MarkOrderDeliveredManuallyCommand(Guid OrderId, string? Reason, string? Evidence) : IRequest<Result>;

/// <summary>Opens a support conversation with the customer about this order.</summary>
public sealed record ContactOrderCustomerCommand(Guid OrderId, string? Subject, string? Message) : IRequest<Result<Guid>>;

internal sealed class MarkOrderDeliveredManuallyCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<MarkOrderDeliveredManuallyCommand, Result>
{
    public async Task<Result> Handle(MarkOrderDeliveredManuallyCommand request, CancellationToken cancellationToken)
    {
        var reason = AdminActionReason.Require(request.Reason);
        if (reason.IsFailure)
        {
            return Result.Failure(reason.Error);
        }

        if (string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Trim().Length < 3)
        {
            return Result.Failure(CommerceErrors.DeliveryEvidenceRequired);
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(CommerceErrors.OrderNotFound);
        }

        if (order.PaymentStatus != PaymentStatus.Paid
            || order.Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.Failed)
        {
            return Result.Failure(CommerceErrors.OrderNotPaidForDelivery);
        }

        var previous = order.Status.ToString();

        // Delivered by hand: lift any hold that was waiting for exactly this, then complete the order.
        var actorId = currentUser.UserId ?? "system";
        var actorName = currentUser.DisplayName ?? actorId;
        order.ReleaseHold(actorId);
        order.Complete();

        var evidence = request.Evidence.Trim();
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "ManuallyMarkedDelivered",
            $"Order marked as delivered by hand. Evidence: {(evidence.Length > 300 ? evidence[..300] : evidence)}",
            actorId,
            actorName,
            reason: reason.Value,
            oldValue: previous,
            newValue: order.Status.ToString()));
        order.RecordAdminEdit(actorId, actorName);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class ContactOrderCustomerCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser,
    IDeliveryTicketService ticketService) : IRequestHandler<ContactOrderCustomerCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ContactOrderCustomerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 3)
        {
            return Result.Failure<Guid>(CommerceErrors.ActionReasonRequired);
        }

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Guid>(CommerceErrors.OrderNotFound);
        }

        var productId = order.Items.FirstOrDefault(i => i.ProductId is not null)?.ProductId ?? Guid.Empty;
        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? $"Order {order.OrderNumber}"
            : request.Subject.Trim();

        var ticketId = await ticketService.CreateDeliveryTicketAsync(
            new DeliveryTicketRequest(order.UserId, order.Id, productId, subject, request.Message.Trim()),
            cancellationToken);

        var actorId = currentUser.UserId ?? "system";
        var actorName = currentUser.DisplayName ?? actorId;
        db.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "CustomerContacted",
            $"A support conversation was opened with the customer (ticket {ticketId}).",
            actorId,
            actorName,
            reason: subject));
        order.RecordAdminEdit(actorId, actorName);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ticketId);
    }
}
