using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand() : IRequest<Result>;

internal sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserNotificationRealtimeNotifier _realtimeNotifier;

    public MarkAllNotificationsReadCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService,
        IUserNotificationRealtimeNotifier realtimeNotifier)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var notifications = await _commerceDbContext.UserNotifications
            .Where(n => n.UserId == _currentUserService.UserId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
        }

        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        if (notifications.Count > 0)
        {
            await _realtimeNotifier.NotifyAllNotificationsReadAsync(_currentUserService.UserId, cancellationToken);
        }

        return Result.Success();
    }
}
