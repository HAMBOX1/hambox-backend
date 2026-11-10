using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Points.GetPointsSummary;

public sealed record GetPointsSummaryQuery : IRequest<Result<PointsSummaryDto>>;

internal sealed class GetPointsSummaryQueryHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser,
    IPlatformSettingsProvider platformSettings)
    : IRequestHandler<GetPointsSummaryQuery, Result<PointsSummaryDto>>
{
    public async Task<Result<PointsSummaryDto>> Handle(GetPointsSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<PointsSummaryDto>(CommerceErrors.AuthenticationRequired);
        }

        var rows = db.PointsTransactions.Where(t => t.UserId == userId);

        // The balance is derived from the ledger, never stored, so it can't disagree with the history.
        var balance = await rows.SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;
        var earned = await rows
            .Where(t => t.Type == PointsTransactionType.Earned)
            .SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;

        var settings = await platformSettings.GetAsync<PurchasePointsSettingsPayload>(
            PlatformSettingsCategoryKeys.PurchasePoints, cancellationToken);

        return Result.Success(new PointsSummaryDto(balance, earned, settings.Enabled, settings.PointsPerUsd));
    }
}
