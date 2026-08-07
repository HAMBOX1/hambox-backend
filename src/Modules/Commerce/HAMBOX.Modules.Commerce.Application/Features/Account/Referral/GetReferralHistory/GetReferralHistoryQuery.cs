using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Referral.GetReferralHistory;

public sealed record GetReferralHistoryQuery(
    int PageNumber,
    int PageSize,
    string? Search,
    string? Status) : IRequest<Result<PagedResult<ReferralHistoryDto>>>;

internal sealed class GetReferralHistoryQueryHandler
    : IRequestHandler<GetReferralHistoryQuery, Result<PagedResult<ReferralHistoryDto>>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetReferralHistoryQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<ReferralHistoryDto>>> Handle(
        GetReferralHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<PagedResult<ReferralHistoryDto>>(CommerceErrors.AuthenticationRequired);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _commerceDbContext.ReferralHistoryEntries
            .Where(h => h.ReferrerUserId == _currentUserService.UserId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(h => h.ReferredEmail.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ReferralStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(h => h.Status == status);
        }

        query = query.OrderByDescending(h => h.CreatedOnUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var entries = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entries.Select(AccountMapper.ToReferralHistoryDto).ToList();

        return Result.Success(new PagedResult<ReferralHistoryDto>(items, pageNumber, pageSize, totalCount));
    }
}
