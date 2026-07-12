using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Memberships.Plans.GetMembershipPlans;

public sealed record GetMembershipPlansQuery(int PageNumber, int PageSize, string? SearchTerm, string? Status)
    : IRequest<Result<PagedResult<MembershipPlanListItemDto>>>;

internal sealed class GetMembershipPlansQueryHandler : IRequestHandler<GetMembershipPlansQuery, Result<PagedResult<MembershipPlanListItemDto>>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetMembershipPlansQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PagedResult<MembershipPlanListItemDto>>> Handle(GetMembershipPlansQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.MembershipPlans.Include(p => p.Benefits).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Slug.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<MembershipPlanStatus>(request.Status, true, out var status))
        {
            query = query.Where(p => p.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var plans = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var counts = await MembershipMapper.GetSubscriberCountsAsync(_dbContext, plans.Select(p => p.Id), cancellationToken);
        var items = plans.Select(p => new MembershipPlanListItemDto(
            p.Id, p.Name, p.Slug, p.Price, p.DurationDays, p.Status.ToString(), p.SortOrder,
            p.BadgeLabel, p.IsDefault, p.Benefits.Count, counts.GetValueOrDefault(p.Id))).ToList();

        return Result.Success(new PagedResult<MembershipPlanListItemDto>(items, request.PageNumber, request.PageSize, total));
    }
}
