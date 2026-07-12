using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Membership;

public sealed record GetMembershipCheckoutPreviewQuery(
    Guid PlanId,
    string Action,
    string? CouponCode = null) : IRequest<Result<MembershipCheckoutPreviewDto>>;

internal sealed class GetMembershipCheckoutPreviewQueryHandler
    : IRequestHandler<GetMembershipCheckoutPreviewQuery, Result<MembershipCheckoutPreviewDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public GetMembershipCheckoutPreviewQueryHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<MembershipCheckoutPreviewDto>> Handle(
        GetMembershipCheckoutPreviewQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<MembershipCheckoutPreviewDto>(CommerceErrors.AuthenticationRequired);
        }

        if (!TryParseAction(request.Action, out var action))
        {
            return Result.Failure<MembershipCheckoutPreviewDto>(CommerceErrors.MembershipCheckoutActionInvalid);
        }

        var plan = await _dbContext.MembershipPlans
            .Include(p => p.Benefits)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.Status == MembershipPlanStatus.Active, cancellationToken);

        if (plan is null)
        {
            return Result.Failure<MembershipCheckoutPreviewDto>(CommerceErrors.MembershipPlanNotFound);
        }

        var validation = await ValidateActionAsync(action, plan, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<MembershipCheckoutPreviewDto>(validation.Error);
        }

        string? currentPlanName = null;
        if (action is not MembershipCheckoutAction.Subscribe)
        {
            var active = await GetActiveSubscriptionAsync(cancellationToken);
            if (active is not null)
            {
                currentPlanName = (await _dbContext.MembershipPlans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == active.PlanId, cancellationToken))?.Name;
            }
        }

        return Result.Success(new MembershipCheckoutPreviewDto(
            plan.Id,
            plan.Name,
            action.ToString(),
            plan.Price,
            plan.DurationDays,
            plan.BadgeLabel,
            plan.Benefits.Count,
            plan.Price > 0,
            currentPlanName));
    }

    private async Task<Result> ValidateActionAsync(
        MembershipCheckoutAction action,
        MembershipPlan plan,
        CancellationToken cancellationToken)
    {
        var active = await GetActiveSubscriptionAsync(cancellationToken);
        var pending = await _dbContext.MembershipSubscriptions
            .AnyAsync(s => s.UserId == _currentUser.UserId && s.Status == MembershipSubscriptionStatus.PendingPayment, cancellationToken);

        return action switch
        {
            MembershipCheckoutAction.Subscribe when active is not null =>
                Result.Failure(CommerceErrors.MembershipSubscriptionAlreadyActive),
            MembershipCheckoutAction.Subscribe when pending =>
                Result.Failure(CommerceErrors.MembershipCheckoutPending),
            MembershipCheckoutAction.Subscribe => Result.Success(),
            MembershipCheckoutAction.Renew when active is null =>
                Result.Failure(CommerceErrors.MembershipSubscriptionNotFound),
            MembershipCheckoutAction.Upgrade or MembershipCheckoutAction.Downgrade when active is null =>
                Result.Failure(CommerceErrors.MembershipSubscriptionNotFound),
            MembershipCheckoutAction.Upgrade or MembershipCheckoutAction.Downgrade when active!.PlanId == plan.Id =>
                Result.Failure(CommerceErrors.MembershipPlanUnchanged),
            MembershipCheckoutAction.Renew => Result.Success(),
            MembershipCheckoutAction.Upgrade or MembershipCheckoutAction.Downgrade => Result.Success(),
            _ => Result.Failure(CommerceErrors.MembershipCheckoutActionInvalid),
        };
    }

    private Task<MembershipSubscription?> GetActiveSubscriptionAsync(CancellationToken cancellationToken) =>
        _dbContext.MembershipSubscriptions
            .FirstOrDefaultAsync(
                s => s.UserId == _currentUser.UserId && s.Status == MembershipSubscriptionStatus.Active,
                cancellationToken);

    private static bool TryParseAction(string action, out MembershipCheckoutAction parsed) =>
        Enum.TryParse(action, ignoreCase: true, out parsed);
}
