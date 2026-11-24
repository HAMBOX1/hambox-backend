using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Points.GetPointsSummary;

public sealed record GetPointsSummaryQuery : IRequest<Result<PointsSummaryDto>>;

internal sealed class GetPointsSummaryQueryHandler(
    ICurrentUserService currentUser,
    IPlatformSettingsProvider platformSettings,
    StarsLedgerService ledger)
    : IRequestHandler<GetPointsSummaryQuery, Result<PointsSummaryDto>>
{
    public async Task<Result<PointsSummaryDto>> Handle(GetPointsSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<PointsSummaryDto>(CommerceErrors.AuthenticationRequired);
        }

        var stars = await ledger.GetBalanceAsync(userId, cancellationToken);

        var settings = await platformSettings.GetAsync<PurchasePointsSettingsPayload>(
            PlatformSettingsCategoryKeys.PurchasePoints, cancellationToken);

        return Result.Success(new PointsSummaryDto(stars.Available, stars.LifetimeEarned, settings.Enabled, settings.PointsPerUsd, stars.Pending, stars.Reserved));
    }
}
