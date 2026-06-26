using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

internal sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationReadCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var notification = await _commerceDbContext.UserNotifications
            .FirstOrDefaultAsync(
                n => n.Id == request.NotificationId && n.UserId == _currentUserService.UserId,
                cancellationToken);

        if (notification is null)
        {
            return Result.Failure(CommerceErrors.NotificationNotFound);
        }

        notification.MarkAsRead();
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
