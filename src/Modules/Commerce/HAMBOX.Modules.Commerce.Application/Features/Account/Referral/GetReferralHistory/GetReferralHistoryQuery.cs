using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Referral.GetReferralHistory;

public sealed record GetReferralHistoryQuery() : IRequest<Result<IReadOnlyList<ReferralHistoryDto>>>;

internal sealed class GetReferralHistoryQueryHandler
    : IRequestHandler<GetReferralHistoryQuery, Result<IReadOnlyList<ReferralHistoryDto>>>
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

    public async Task<Result<IReadOnlyList<ReferralHistoryDto>>> Handle(
        GetReferralHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<IReadOnlyList<ReferralHistoryDto>>(CommerceErrors.AuthenticationRequired);
        }

        var history = await _commerceDbContext.ReferralHistoryEntries
            .Where(h => h.ReferrerUserId == _currentUserService.UserId)
            .OrderByDescending(h => h.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReferralHistoryDto>>(
            history.Select(AccountMapper.ToReferralHistoryDto).ToList());
    }
}
