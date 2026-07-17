using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

internal sealed class GetBlockedUsersQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetBlockedUsersQuery, Result<PagedResult<BlockedUserListItemDto>>>
{
    private static readonly UserStatus[] RestrictedStatuses =
        [UserStatus.Suspended, UserStatus.Blocked, UserStatus.Banned];

    public async Task<Result<PagedResult<BlockedUserListItemDto>>> Handle(
        GetBlockedUsersQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().Where(u => RestrictedStatuses.Contains(u.Status));

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(u =>
                u.Email.Contains(term) ||
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.ModifiedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new BlockedUserListItemDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Status.ToString(),
                u.BlockReason,
                u.BlockNotes,
                u.BlockExpiresOnUtc,
                u.ModifiedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<BlockedUserListItemDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
