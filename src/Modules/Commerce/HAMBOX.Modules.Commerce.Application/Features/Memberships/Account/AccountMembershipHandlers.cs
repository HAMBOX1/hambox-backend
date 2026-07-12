using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Memberships.Account;

public sealed record GetCurrentMembershipQuery() : IRequest<Result<CurrentMembershipDto>>;
public sealed record GetMembershipHistoryQuery() : IRequest<Result<IReadOnlyList<MembershipHistoryDto>>>;
public sealed record GetMembershipTransactionsQuery() : IRequest<Result<IReadOnlyList<MembershipTransactionDto>>>;
public sealed record CustomerUpgradeMembershipCommand(ChangeMembershipPlanRequest Request) : IRequest<Result<MembershipSubscriptionDto>>;
public sealed record CustomerDowngradeMembershipCommand(ChangeMembershipPlanRequest Request) : IRequest<Result<MembershipSubscriptionDto>>;
public sealed record CustomerRenewMembershipCommand() : IRequest<Result<MembershipSubscriptionDto>>;
public sealed record CustomerSubscribeMembershipCommand(ChangeMembershipPlanRequest Request) : IRequest<Result<MembershipSubscriptionDto>>;

internal sealed class AccountMembershipHandlers :
    IRequestHandler<GetCurrentMembershipQuery, Result<CurrentMembershipDto>>,
    IRequestHandler<GetMembershipHistoryQuery, Result<IReadOnlyList<MembershipHistoryDto>>>,
    IRequestHandler<GetMembershipTransactionsQuery, Result<IReadOnlyList<MembershipTransactionDto>>>,
    IRequestHandler<CustomerUpgradeMembershipCommand, Result<MembershipSubscriptionDto>>,
    IRequestHandler<CustomerDowngradeMembershipCommand, Result<MembershipSubscriptionDto>>,
    IRequestHandler<CustomerRenewMembershipCommand, Result<MembershipSubscriptionDto>>,
    IRequestHandler<CustomerSubscribeMembershipCommand, Result<MembershipSubscriptionDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IMembershipEngine _membershipEngine;
    private readonly MembershipOperationsService _operations;

    public AccountMembershipHandlers(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUser,
        IMembershipEngine membershipEngine,
        MembershipOperationsService operations)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _membershipEngine = membershipEngine;
        _operations = operations;
    }

    public async Task<Result<CurrentMembershipDto>> Handle(GetCurrentMembershipQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<CurrentMembershipDto>(CommerceErrors.AuthenticationRequired);
        }

        var snapshot = await _membershipEngine.ResolveAsync(_currentUser.UserId, cancellationToken);
        var subscription = await _dbContext.MembershipSubscriptions
            .Where(s => s.UserId == _currentUser.UserId &&
                (s.Status == MembershipSubscriptionStatus.Active ||
                 s.Status == MembershipSubscriptionStatus.PendingPayment))
            .OrderByDescending(s => s.ExpiresOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        MembershipSubscriptionDto? subDto = null;
        if (subscription is not null)
        {
            var plan = await _dbContext.MembershipPlans.FirstAsync(p => p.Id == subscription.PlanId, cancellationToken);
            subDto = MembershipMapper.ToSubscriptionDto(subscription, plan);
        }

        var plans = await _dbContext.MembershipPlans.Include(p => p.Benefits)
            .Where(p => p.Status == MembershipPlanStatus.Active)
            .OrderBy(p => p.SortOrder).ToListAsync(cancellationToken);
        var counts = await MembershipMapper.GetSubscriberCountsAsync(_dbContext, plans.Select(p => p.Id), cancellationToken);
        var planDtos = plans.Select(p => new MembershipPlanListItemDto(
            p.Id, p.Name, p.Slug, p.Price, p.DurationDays, p.Status.ToString(), p.SortOrder,
            p.BadgeLabel, p.IsDefault, p.Benefits.Count, counts.GetValueOrDefault(p.Id))).ToList();

        return Result.Success(new CurrentMembershipDto(
            subDto,
            snapshot.Benefits.Select(b => new MembershipBenefitDto(b.Type, b.Value, b.DisplayName, 0)).ToList(),
            planDtos));
    }

    public async Task<Result<IReadOnlyList<MembershipHistoryDto>>> Handle(GetMembershipHistoryQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<IReadOnlyList<MembershipHistoryDto>>(CommerceErrors.AuthenticationRequired);
        }

        var history = await _dbContext.MembershipHistories.Where(h => h.UserId == _currentUser.UserId)
            .OrderByDescending(h => h.OccurredOnUtc).Take(50).ToListAsync(cancellationToken);
        var planIds = history.Select(h => h.PlanId).Distinct().ToList();
        var plans = await _dbContext.MembershipPlans.Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        return Result.Success<IReadOnlyList<MembershipHistoryDto>>(history.Select(h => MembershipMapper.ToHistoryDto(h, plans[h.PlanId].Name)).ToList());
    }

    public async Task<Result<IReadOnlyList<MembershipTransactionDto>>> Handle(GetMembershipTransactionsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<IReadOnlyList<MembershipTransactionDto>>(CommerceErrors.AuthenticationRequired);
        }

        var txs = await _dbContext.MembershipTransactions.Where(t => t.UserId == _currentUser.UserId)
            .OrderByDescending(t => t.CreatedOnUtc).Take(50).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<MembershipTransactionDto>>(txs.Select(t => new MembershipTransactionDto(t.Id, t.PlanId, t.Amount, t.Status.ToString(), t.CreatedOnUtc)).ToList());
    }

    public async Task<Result<MembershipSubscriptionDto>> Handle(CustomerUpgradeMembershipCommand command, CancellationToken cancellationToken) =>
        Result.Failure<MembershipSubscriptionDto>(CommerceErrors.MembershipCheckoutRequired);

    public async Task<Result<MembershipSubscriptionDto>> Handle(CustomerDowngradeMembershipCommand command, CancellationToken cancellationToken) =>
        Result.Failure<MembershipSubscriptionDto>(CommerceErrors.MembershipCheckoutRequired);

    public async Task<Result<MembershipSubscriptionDto>> Handle(CustomerRenewMembershipCommand request, CancellationToken cancellationToken) =>
        Result.Failure<MembershipSubscriptionDto>(CommerceErrors.MembershipCheckoutRequired);

    public async Task<Result<MembershipSubscriptionDto>> Handle(
        CustomerSubscribeMembershipCommand command,
        CancellationToken cancellationToken) =>
        Result.Failure<MembershipSubscriptionDto>(CommerceErrors.MembershipCheckoutRequired);
}
