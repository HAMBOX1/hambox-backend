using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.ClaimGuestAlertSubscriptions;

/// <summary>
/// Reassigns every alert subscription created anonymously under <see cref="GuestSessionId"/> to the
/// now-authenticated caller — the same claim-on-login shape as <c>MergeCartCommandHandler</c>. Called
/// unconditionally after every login (mirrors <c>mergeGuestCartIfNeeded</c>); a missing or empty guest
/// session is a harmless no-op, never an error.
/// </summary>
public sealed record ClaimGuestAlertSubscriptionsCommand(string? GuestSessionId) : IRequest<Result<int>>;

internal sealed class ClaimGuestAlertSubscriptionsCommandHandler : IRequestHandler<ClaimGuestAlertSubscriptionsCommand, Result<int>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public ClaimGuestAlertSubscriptionsCommandHandler(ICommerceDbContext commerceDbContext, ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> Handle(ClaimGuestAlertSubscriptionsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<int>(CommerceErrors.AuthenticationRequired);
        }

        if (string.IsNullOrWhiteSpace(request.GuestSessionId))
        {
            return Result.Success(0);
        }

        var userId = _currentUserService.UserId;

        var guestSubscriptions = await _commerceDbContext.CustomerAlertSubscriptions
            .Where(s => s.GuestSessionId == request.GuestSessionId && s.IsActive)
            .ToListAsync(cancellationToken);

        if (guestSubscriptions.Count == 0)
        {
            return Result.Success(0);
        }

        var existingKeys = await _commerceDbContext.CustomerAlertSubscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .Select(s => new { s.VariantId, s.AlertType })
            .ToListAsync(cancellationToken);
        var existingSet = existingKeys.Select(k => (k.VariantId, k.AlertType)).ToHashSet();

        var claimed = 0;
        foreach (var subscription in guestSubscriptions)
        {
            // The user already has their own active subscription for this (variant, type) — claiming
            // the guest row too would collide with the unique index, so drop the redundant guest copy
            // instead. Prevents duplicate active subscriptions across the anonymous-to-authenticated
            // boundary, same guarantee as the single-owner unique index enforces within one identity.
            if (!existingSet.Add((subscription.VariantId, subscription.AlertType)))
            {
                _commerceDbContext.CustomerAlertSubscriptions.Remove(subscription);
                continue;
            }

            subscription.ClaimFor(userId);
            claimed++;
        }

        await _commerceDbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(claimed);
    }
}
