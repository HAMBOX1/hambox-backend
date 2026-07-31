using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.GetRoleById;

internal sealed class GetRoleByIdQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetRoleByIdQuery, Result<RoleDetailDto>>
{
    public async Task<Result<RoleDetailDto>> Handle(
        GetRoleByIdQuery request,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.Id == request.RoleId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return Result.Failure<RoleDetailDto>(IdentityErrors.RoleNotFound);
        }

        var userCount = await dbContext.UserRoles
            .CountAsync(ur => ur.RoleId == request.RoleId, cancellationToken);

        var permissionIds = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == request.RoleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        return Result.Success(new RoleDetailDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystem,
            role.IsDefault,
            role.PriorityLevel,
            userCount,
            permissionIds,
            role.CreatedOnUtc,
            role.ModifiedOnUtc));
    }
}
