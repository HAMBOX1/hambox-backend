using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.AssignAdminOrderManualCode;

public sealed record AssignAdminOrderManualCodeCommand(Guid OrderId, AssignAdminOrderManualCodeRequest Request)
    : IRequest<Result<AdminOrderDetailDto>>;

internal sealed class AssignAdminOrderManualCodeCommandHandler
    : IRequestHandler<AssignAdminOrderManualCodeCommand, Result<AdminOrderDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly OrderFulfillmentService _fulfillmentService;
    private readonly ISender _sender;

    public AssignAdminOrderManualCodeCommandHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService,
        OrderFulfillmentService fulfillmentService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _fulfillmentService = fulfillmentService;
        _sender = sender;
    }

    public async Task<Result<AdminOrderDetailDto>> Handle(
        AssignAdminOrderManualCodeCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.LicenseKey))
        {
            return Result.Failure<AdminOrderDetailDto>(
                CommerceErrors.OrderManualCodeInvalid("License key is required."));
        }

        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderNotFound);
        }

        try
        {
            await _fulfillmentService.AssignManualCodeAsync(
                order,
                command.Request.OrderItemId,
                command.Request.LicenseKey,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderManualCodeInvalid(ex.Message));
        }

        var actorId = _currentUserService.UserId ?? "system";
        _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "ManualCodeAssigned",
            "A digital code was manually assigned to an order line item.",
            actorId,
            actorId));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetAdminOrderById.GetAdminOrderByIdQuery(order.Id), cancellationToken);
    }
}
