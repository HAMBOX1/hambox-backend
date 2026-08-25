using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferralById;

internal sealed class GetAdminReferralByIdQueryHandler
    : IRequestHandler<GetAdminReferralByIdQuery, Result<AdminReferralDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetAdminReferralByIdQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<AdminReferralDetailDto>> Handle(
        GetAdminReferralByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entry = await _dbContext.ReferralHistoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == request.ReferralId, cancellationToken);

        if (entry is null)
        {
            return Result.Failure<AdminReferralDetailDto>(CommerceErrors.ReferralNotFound);
        }

        var referralCode = await _dbContext.ReferralProfiles
            .AsNoTracking()
            .Where(p => p.UserId == entry.ReferrerUserId)
            .Select(p => p.ReferralCode)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var auditLogs = await _dbContext.ReferralAuditLogs
            .AsNoTracking()
            .Where(a => a.ReferralHistoryEntryId == entry.Id)
            .OrderByDescending(a => a.OccurredOnUtc)
            .ToListAsync(cancellationToken);

        return Result.Success(new AdminReferralDetailDto(
            AdminReferralMapper.ToListItem(entry, referralCode),
            auditLogs.Select(AdminReferralMapper.ToAuditEntry).ToList()));
    }
}
