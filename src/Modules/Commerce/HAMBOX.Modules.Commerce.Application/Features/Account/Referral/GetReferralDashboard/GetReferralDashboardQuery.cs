using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Referral.GetReferralDashboard;

public sealed record GetReferralDashboardQuery() : IRequest<Result<ReferralDashboardDto>>;

internal sealed class GetReferralDashboardQueryHandler : IRequestHandler<GetReferralDashboardQuery, Result<ReferralDashboardDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetReferralDashboardQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ReferralDashboardDto>> Handle(
        GetReferralDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<ReferralDashboardDto>(CommerceErrors.AuthenticationRequired);
        }

        var profile = await _commerceDbContext.ReferralProfiles
            .FirstOrDefaultAsync(r => r.UserId == _currentUserService.UserId, cancellationToken);

        if (profile is null)
        {
            profile = ReferralProfile.CreateForUser(_currentUserService.UserId);
            _commerceDbContext.ReferralProfiles.Add(profile);
            await _commerceDbContext.SaveChangesAsync(cancellationToken);
        }

        var recentHistory = await _commerceDbContext.ReferralHistoryEntries
            .Where(h => h.ReferrerUserId == _currentUserService.UserId)
            .OrderByDescending(h => h.CreatedOnUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        return Result.Success(AccountMapper.ToReferralDashboardDto(profile, recentHistory));
    }
}
