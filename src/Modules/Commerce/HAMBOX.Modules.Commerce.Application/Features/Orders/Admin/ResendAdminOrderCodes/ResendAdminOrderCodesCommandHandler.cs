using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.ResendAdminOrderCodes;

public sealed record ResendAdminOrderCodesCommand(Guid OrderId) : IRequest<Result>;

internal sealed class ResendAdminOrderCodesCommandHandler : IRequestHandler<ResendAdminOrderCodesCommand, Result>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICommunicationService _communicationService;

    public ResendAdminOrderCodesCommandHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService,
        ICommunicationService communicationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _communicationService = communicationService;
    }

    public async Task<Result> Handle(ResendAdminOrderCodesCommand command, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(CommerceErrors.OrderNotFound);
        }

        var keys = await _dbContext.OrderLicenseKeys
            .AsNoTracking()
            .Where(k => k.OrderId == command.OrderId)
            .ToListAsync(cancellationToken);

        if (keys.Count == 0)
        {
            return Result.Failure(CommerceErrors.OrderLicenseKeyNotFound);
        }

        var actorId = _currentUserService.UserId ?? "system";
        var actorName = _currentUserService.DisplayName ?? actorId;
        _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "CodesResent",
            "Digital delivery codes were resent to the customer.",
            actorId,
            actorName));
        order.RecordAdminEdit(actorId, actorName);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _communicationService.SendAsync(new CommunicationRequest(
            UserId: order.UserId,
            TemplateKey: "CodesResent",
            Category: CommunicationCategory.Order,
            Variables: new Dictionary<string, string>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["CodeCount"] = keys.Count.ToString(),
            },
            RelatedEntityType: "Order",
            RelatedEntityId: order.Id.ToString(),
            ActionUrl: $"/account/library?orderId={order.Id}"), cancellationToken);

        return Result.Success();
    }
}
