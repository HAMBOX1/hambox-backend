using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Memberships.Subscriptions;

public sealed record AssignMembershipCommand(AssignMembershipRequest Request) : IRequest<Result<MembershipSubscriptionDto>>;
public sealed record BulkAssignMembershipCommand(BulkAssignMembershipRequest Request) : IRequest<Result<int>>;
public sealed record RenewMembershipCommand(Guid SubscriptionId) : IRequest<Result<MembershipSubscriptionDto>>;
public sealed record UpgradeMembershipCommand(Guid SubscriptionId, ChangeMembershipPlanRequest Request) : IRequest<Result<MembershipSubscriptionDto>>;
public sealed record DowngradeMembershipCommand(Guid SubscriptionId, ChangeMembershipPlanRequest Request) : IRequest<Result<MembershipSubscriptionDto>>;
public sealed record CancelMembershipCommand(Guid SubscriptionId) : IRequest<Result>;
public sealed record GetMembershipStatisticsQuery() : IRequest<Result<MembershipStatisticsDto>>;
public sealed record GetMembershipMembersQuery(int PageNumber, int PageSize, string? SearchTerm) : IRequest<Result<PagedResult<MemberListItemDto>>>;

internal sealed class MembershipSubscriptionHandlers :
    IRequestHandler<AssignMembershipCommand, Result<MembershipSubscriptionDto>>,
    IRequestHandler<BulkAssignMembershipCommand, Result<int>>,
    IRequestHandler<RenewMembershipCommand, Result<MembershipSubscriptionDto>>,
    IRequestHandler<UpgradeMembershipCommand, Result<MembershipSubscriptionDto>>,
    IRequestHandler<DowngradeMembershipCommand, Result<MembershipSubscriptionDto>>,
    IRequestHandler<CancelMembershipCommand, Result>,
    IRequestHandler<GetMembershipStatisticsQuery, Result<MembershipStatisticsDto>>,
    IRequestHandler<GetMembershipMembersQuery, Result<PagedResult<MemberListItemDto>>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly MembershipOperationsService _operations;
    private readonly IUserDirectory _userDirectory;

    private const int MaxBulkAssignUsers = 500;

    public MembershipSubscriptionHandlers(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUser,
        MembershipOperationsService operations,
        IUserDirectory userDirectory)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _operations = operations;
        _userDirectory = userDirectory;
    }

    /// <summary>
    /// The membership operations signal a business rule ("plan is not active", "cancelled subscriptions cannot be
    /// renewed") by throwing; turn that into a normal 400 instead of an unhandled 500.
    /// </summary>
    private static async Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<T>(CommerceErrors.MembershipOperationRejected(exception.Message));
        }
    }

    public Task<Result<MembershipSubscriptionDto>> Handle(AssignMembershipCommand command, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var existing = await _userDirectory.GetExistingUserIdsAsync([command.Request.UserId ?? string.Empty], cancellationToken);
            if (!existing.Contains(command.Request.UserId ?? string.Empty))
            {
                return Result.Failure<MembershipSubscriptionDto>(CommerceErrors.MembershipUserNotFound);
            }

            var subscription = await _operations.AssignAsync(command.Request.UserId!, command.Request.PlanId, command.Request.AutoRenew, _currentUser.UserId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var plan = await _dbContext.MembershipPlans.FirstAsync(p => p.Id == subscription.PlanId, cancellationToken);
            return Result.Success(MembershipMapper.ToSubscriptionDto(subscription, plan));
        });

    public Task<Result<int>> Handle(BulkAssignMembershipCommand command, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var userIds = (command.Request.UserIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (userIds.Count > MaxBulkAssignUsers)
            {
                return Result.Failure<int>(CommerceErrors.MembershipTooManyUsers);
            }

            // Nothing is assigned unless every id is a real account: no orphan subscriptions for typos.
            var existing = await _userDirectory.GetExistingUserIdsAsync(userIds, cancellationToken);
            if (userIds.Any(id => !existing.Contains(id)))
            {
                return Result.Failure<int>(CommerceErrors.MembershipUserNotFound);
            }

            var count = 0;
            foreach (var userId in userIds)
            {
                await _operations.AssignAsync(userId, command.Request.PlanId, command.Request.AutoRenew, _currentUser.UserId, cancellationToken);
                count++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(count);
        });

    public Task<Result<MembershipSubscriptionDto>> Handle(RenewMembershipCommand command, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var subscription = await _operations.RenewAsync(command.SubscriptionId, _currentUser.UserId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var plan = await _dbContext.MembershipPlans.FirstAsync(p => p.Id == subscription.PlanId, cancellationToken);
            return Result.Success(MembershipMapper.ToSubscriptionDto(subscription, plan));
        });

    public Task<Result<MembershipSubscriptionDto>> Handle(UpgradeMembershipCommand command, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var subscription = await _operations.ChangePlanAsync(command.SubscriptionId, command.Request.TargetPlanId, MembershipHistoryAction.Upgraded, _currentUser.UserId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var plan = await _dbContext.MembershipPlans.FirstAsync(p => p.Id == subscription.PlanId, cancellationToken);
            return Result.Success(MembershipMapper.ToSubscriptionDto(subscription, plan));
        });

    public Task<Result<MembershipSubscriptionDto>> Handle(DowngradeMembershipCommand command, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            var subscription = await _operations.ChangePlanAsync(command.SubscriptionId, command.Request.TargetPlanId, MembershipHistoryAction.Downgraded, _currentUser.UserId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var plan = await _dbContext.MembershipPlans.FirstAsync(p => p.Id == subscription.PlanId, cancellationToken);
            return Result.Success(MembershipMapper.ToSubscriptionDto(subscription, plan));
        });

    public async Task<Result> Handle(CancelMembershipCommand command, CancellationToken cancellationToken)
    {
        var result = await GuardAsync(async () =>
        {
            await _operations.CancelAsync(command.SubscriptionId, _currentUser.UserId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        });
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    public async Task<Result<MembershipStatisticsDto>> Handle(GetMembershipStatisticsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var week = now.AddDays(7);
        var totalPlans = await _dbContext.MembershipPlans.CountAsync(cancellationToken);
        var activePlans = await _dbContext.MembershipPlans.CountAsync(p => p.Status == MembershipPlanStatus.Active, cancellationToken);
        var activeSubs = await _dbContext.MembershipSubscriptions.CountAsync(s => s.Status == MembershipSubscriptionStatus.Active, cancellationToken);
        var expiring = await _dbContext.MembershipSubscriptions.CountAsync(s => s.Status == MembershipSubscriptionStatus.Active && s.ExpiresOnUtc <= week, cancellationToken);
        var mrr = await _dbContext.MembershipPlans.Where(p => p.Status == MembershipPlanStatus.Active)
            .Join(_dbContext.MembershipSubscriptions.Where(s => s.Status == MembershipSubscriptionStatus.Active),
                p => p.Id, s => s.PlanId, (p, _) => p.Price)
            .SumAsync(cancellationToken);
        return Result.Success(new MembershipStatisticsDto(totalPlans, activePlans, activeSubs, expiring, mrr));
    }

    public async Task<Result<PagedResult<MemberListItemDto>>> Handle(GetMembershipMembersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<MembershipSubscription> query = _dbContext.MembershipSubscriptions.AsNoTracking()
            .Where(s => s.Status == MembershipSubscriptionStatus.Active);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(s => s.UserId.Contains(term));
        }

        query = query.OrderByDescending(s => s.ExpiresOnUtc);

        var total = await query.CountAsync(cancellationToken);
        var subs = await query.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        var planIds = subs.Select(s => s.PlanId).Distinct().ToList();
        var plans = await _dbContext.MembershipPlans.Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var items = subs.Select(s => new MemberListItemDto(s.UserId, s.Id, s.PlanId, plans[s.PlanId].Name, s.Status.ToString(), s.ExpiresOnUtc)).ToList();
        return Result.Success(new PagedResult<MemberListItemDto>(items, request.PageNumber, request.PageSize, total));
    }
}
