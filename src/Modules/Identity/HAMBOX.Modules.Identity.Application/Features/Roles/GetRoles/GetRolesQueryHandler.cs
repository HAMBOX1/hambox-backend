using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoles;

internal sealed class GetRolesQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetRolesQuery, Result<PagedResult<RoleListItemDto>>>
{
    public async Task<Result<PagedResult<RoleListItemDto>>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(r =>
                r.Name.Contains(term) ||
                (r.Description != null && r.Description.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var roles = await query
            .OrderBy(r => r.PriorityLevel)
            .ThenBy(r => r.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.IsSystem,
                r.IsDefault,
                r.PriorityLevel,
                r.CreatedOnUtc,
                r.ModifiedOnUtc,
            })
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(r => r.Id).ToList();

        var userCounts = await dbContext.UserRoles
            .Where(ur => roleIds.Contains(ur.RoleId))
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var permissionCounts = await dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var items = roles.Select(r => new RoleListItemDto(
            r.Id,
            r.Name,
            r.Description,
            r.IsSystem,
            r.IsDefault,
            r.PriorityLevel,
            userCounts.GetValueOrDefault(r.Id),
            permissionCounts.GetValueOrDefault(r.Id),
            r.CreatedOnUtc,
            r.ModifiedOnUtc)).ToList();

        return Result.Success(new PagedResult<RoleListItemDto>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount));
    }
}
