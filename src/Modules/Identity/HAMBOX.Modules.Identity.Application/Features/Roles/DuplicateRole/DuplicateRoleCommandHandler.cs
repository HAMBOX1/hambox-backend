using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Roles;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.DuplicateRole;

internal sealed class DuplicateRoleCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IRbacAuthorizationService rbacAuthorizationService,
    IAuthorizationAuditService auditService) : IRequestHandler<DuplicateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(DuplicateRoleCommand request, CancellationToken cancellationToken)
    {
        var actorResult = RoleActorContext.GetActorUserId(currentUserService);
        if (actorResult.IsFailure)
        {
            return Result.Failure<Guid>(actorResult.Error);
        }

        var sourceRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (sourceRole is null)
        {
            return Result.Failure<Guid>(IdentityErrors.RoleNotFound);
        }

        if (!await rbacAuthorizationService.CanManageRoleAsync(actorResult.Value, sourceRole.Id, cancellationToken))
        {
            return Result.Failure<Guid>(IdentityErrors.InsufficientPrivileges);
        }

        var newName = string.IsNullOrWhiteSpace(request.NewName)
            ? $"{sourceRole.Name} Copy"
            : request.NewName.Trim();

        var normalizedName = newName.ToUpperInvariant();
        var nameExists = await dbContext.Roles
            .AnyAsync(r => r.NormalizedName == normalizedName, cancellationToken);

        if (nameExists)
        {
            return Result.Failure<Guid>(IdentityErrors.RoleNameExists);
        }

        var duplicate = ApplicationRole.Create(
            newName,
            sourceRole.Description,
            isDefault: false,
            isSystem: false,
            priorityLevel: sourceRole.PriorityLevel);

        dbContext.Roles.Add(duplicate);
        await dbContext.SaveChangesAsync(cancellationToken);

        var permissionIds = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == sourceRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in permissionIds)
        {
            dbContext.RolePermissions.Add(RolePermission.Create(duplicate.Id, permissionId));
        }

        if (permissionIds.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditService.RecordAsync(
            AuthorizationAuditActions.RoleDuplicated,
            "Role",
            duplicate.Id,
            actorResult.Value,
            $"Duplicated role '{sourceRole.Name}' to '{duplicate.Name}'.",
            request.IpAddress,
            cancellationToken);

        return Result.Success(duplicate.Id);
    }
}
