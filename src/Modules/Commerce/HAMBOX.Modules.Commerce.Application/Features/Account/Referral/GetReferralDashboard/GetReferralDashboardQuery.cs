using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
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
    private readonly IPlatformSettingsProvider _platformSettings;

    public GetReferralDashboardQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService,
        IPlatformSettingsProvider platformSettings)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
        _platformSettings = platformSettings;
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

        var pendingReferrals = await _commerceDbContext.ReferralHistoryEntries
            .CountAsync(h => h.ReferrerUserId == _currentUserService.UserId && h.Status == ReferralStatus.Pending, cancellationToken);

        var referralSettings = await _platformSettings.GetAsync<ReferralSettingsPayload>(
            PlatformSettingsCategoryKeys.Referral, cancellationToken);

        return Result.Success(AccountMapper.ToReferralDashboardDto(
            profile, recentHistory, pendingReferrals, referralSettings.PointsPerReferral, referralSettings.PointValueUsd));
    }
}
