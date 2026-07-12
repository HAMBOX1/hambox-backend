using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.SearchUsers;

internal sealed class SearchUsersQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<SearchUsersQuery, Result<PagedResult<UserSearchResultDto>>>
{
    public async Task<Result<PagedResult<UserSearchResultDto>>> Handle(
        SearchUsersQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().AsQueryable();

        if (request.ExcludeRoleId.HasValue)
        {
            var assignedUserIds = dbContext.UserRoles
                .Where(ur => ur.RoleId == request.ExcludeRoleId.Value)
                .Select(ur => ur.UserId);

            query = query.Where(u => !assignedUserIds.Contains(u.Id));
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

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserSearchResultDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<UserSearchResultDto>(
            users,
            request.PageNumber,
            request.PageSize,
            totalCount));
    }
}
