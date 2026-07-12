using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Account;
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

    public ResendAdminOrderCodesCommandHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(ResendAdminOrderCodesCommand command, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
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

        var body = keys.Count == 1
            ? "Your digital product code has been resent. Open your library to view it."
            : $"{keys.Count} digital product codes were resent. Open your library to view them.";

        _dbContext.UserNotifications.Add(UserNotification.Create(
            order.UserId,
            "Digital codes resent",
            body,
            "Orders",
            $"/account/library?orderId={order.Id}"));

        var actorId = _currentUserService.UserId ?? "system";
        _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            order.Id,
            "CodesResent",
            "Digital delivery codes were resent to the customer.",
            actorId,
            actorId));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
