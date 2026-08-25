using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferrals;

internal sealed class GetAdminReferralsQueryHandler
    : IRequestHandler<GetAdminReferralsQuery, Result<PagedResult<AdminReferralListItemDto>>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetAdminReferralsQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PagedResult<AdminReferralListItemDto>>> Handle(
        GetAdminReferralsQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _dbContext.ReferralHistoryEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            var matchingCodes = _dbContext.ReferralProfiles
                .AsNoTracking()
                .Where(p => p.ReferralCode.Contains(term))
                .Select(p => p.UserId);

            query = query.Where(h =>
                h.ReferredEmail.Contains(term) ||
                matchingCodes.Contains(h.ReferrerUserId));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<ReferralStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(h => h.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderByDescending(h => h.CreatedOnUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var referrerIds = page.Select(h => h.ReferrerUserId).Distinct().ToList();
        var codesByReferrer = await _dbContext.ReferralProfiles
            .AsNoTracking()
            .Where(p => referrerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.ReferralCode, cancellationToken);

        var items = page
            .Select(h => AdminReferralMapper.ToListItem(
                h,
                codesByReferrer.TryGetValue(h.ReferrerUserId, out var code) ? code : string.Empty))
            .ToList();

        return Result.Success(new PagedResult<AdminReferralListItemDto>(items, pageNumber, pageSize, totalCount));
    }
}
