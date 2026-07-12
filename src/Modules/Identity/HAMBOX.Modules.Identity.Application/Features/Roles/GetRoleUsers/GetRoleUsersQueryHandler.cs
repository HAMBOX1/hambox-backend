using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleUsers;

internal sealed class GetRoleUsersQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetRoleUsersQuery, Result<PagedResult<UserSearchResultDto>>>
{
    public async Task<Result<PagedResult<UserSearchResultDto>>> Handle(
        GetRoleUsersQuery request,
        CancellationToken cancellationToken)
    {
        var roleExists = await dbContext.Roles
            .AnyAsync(r => r.Id == request.RoleId, cancellationToken);

        if (!roleExists)
        {
            return Result.Failure<PagedResult<UserSearchResultDto>>(IdentityErrors.RoleNotFound);
        }

        var query =
            from ur in dbContext.UserRoles
            join u in dbContext.Users on ur.UserId equals u.Id
            where ur.RoleId == request.RoleId
            select u;

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
