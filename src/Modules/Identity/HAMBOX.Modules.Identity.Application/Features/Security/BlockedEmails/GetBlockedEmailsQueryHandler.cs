using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;

internal sealed class GetBlockedEmailsQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetBlockedEmailsQuery, Result<PagedResult<BlockedEmailDto>>>
{
    public async Task<Result<PagedResult<BlockedEmailDto>>> Handle(
        GetBlockedEmailsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.BlockedEmails.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(b => b.Pattern.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => new BlockedEmailDto(
                b.Id, b.Pattern, b.Pattern.StartsWith("*@"), b.Reason, b.Notes, b.ExpiresOnUtc, b.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BlockedEmailDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
