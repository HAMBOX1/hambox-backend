using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.DeleteNotification;

public sealed record DeleteNotificationCommand(Guid NotificationId) : IRequest<Result>;

internal sealed class DeleteNotificationCommandHandler(
    ICommerceDbContext commerceDbContext,
    ICurrentUserService currentUserService) : IRequestHandler<DeleteNotificationCommand, Result>
{
    public async Task<Result> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var notification = await commerceDbContext.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == currentUserService.UserId, cancellationToken);

        if (notification is null)
        {
            return Result.Failure(CommerceErrors.NotificationNotFound);
        }

        commerceDbContext.UserNotifications.Remove(notification);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
