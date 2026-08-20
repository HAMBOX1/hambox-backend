using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.RemoveAlertSubscription;

/// <summary>
/// Deletes one alert subscription. Ownership-scoped the same way Wishlist is: the lookup filters by
/// the caller's own identity (authenticated <c>UserId</c>, or an anonymous <c>GuestSessionId</c> for
/// a not-yet-claimed guest subscription) — another owner's row for the same id is simply not found,
/// never a distinguishable 403.
/// </summary>
public sealed record RemoveAlertSubscriptionCommand(Guid Id, string? GuestSessionId) : IRequest<Result>;

internal sealed class RemoveAlertSubscriptionCommandHandler : IRequestHandler<RemoveAlertSubscriptionCommand, Result>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public RemoveAlertSubscriptionCommandHandler(ICommerceDbContext commerceDbContext, ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RemoveAlertSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var isAuthenticated = _currentUserService.IsAuthenticated && _currentUserService.UserId is not null;
        var guestSessionId = string.IsNullOrWhiteSpace(request.GuestSessionId) ? null : request.GuestSessionId;

        if (!isAuthenticated && guestSessionId is null)
        {
            return Result.Failure(CommerceErrors.AlertSubscriptionOwnerRequired);
        }

        var subscription = isAuthenticated
            ? await _commerceDbContext.CustomerAlertSubscriptions
                .FirstOrDefaultAsync(s => s.Id == request.Id && s.UserId == _currentUserService.UserId, cancellationToken)
            : await _commerceDbContext.CustomerAlertSubscriptions
                .FirstOrDefaultAsync(s => s.Id == request.Id && s.GuestSessionId == guestSessionId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure(CommerceErrors.AlertSubscriptionNotFound);
        }

        _commerceDbContext.CustomerAlertSubscriptions.Remove(subscription);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
