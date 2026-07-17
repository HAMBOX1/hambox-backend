using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;

internal sealed class GetBlockedIpsQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetBlockedIpsQuery, Result<PagedResult<BlockedIpDto>>>
{
    public async Task<Result<PagedResult<BlockedIpDto>>> Handle(
        GetBlockedIpsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.BlockedIps.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(b => b.CidrOrAddress.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => new BlockedIpDto(
                b.Id, b.CidrOrAddress, b.Reason, b.Notes, b.ExpiresOnUtc, b.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BlockedIpDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
