using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.UpdateRole;

internal sealed class UpdateRoleCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IRbacAuthorizationService rbacAuthorizationService,
    IAuthorizationAuditService auditService) : IRequestHandler<UpdateRoleCommand, Result>
{
    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var actorResult = RoleActorContext.GetActorUserId(currentUserService);
        if (actorResult.IsFailure)
        {
            return Result.Failure(actorResult.Error);
        }

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(IdentityErrors.RoleNotFound);
        }

        if (RoleConstants.IsOwnerRole(role.Name))
        {
            return Result.Failure(IdentityErrors.CannotModifyOwnerRole);
        }

        if (!await rbacAuthorizationService.CanManageRoleAsync(actorResult.Value, role.Id, cancellationToken))
        {
            return Result.Failure(IdentityErrors.InsufficientPrivileges);
        }

        var normalizedName = request.Name.ToUpperInvariant();
        var nameExists = await dbContext.Roles
            .AnyAsync(r => r.NormalizedName == normalizedName && r.Id != role.Id, cancellationToken);

        if (nameExists)
        {
            return Result.Failure(IdentityErrors.RoleNameExists);
        }

        role.UpdateDetails(request.Name, request.Description);

        if (request.PriorityLevel.HasValue)
        {
            role.SetPriorityLevel(request.PriorityLevel.Value);
        }

        if (request.IsDefault == true)
        {
            var currentDefaults = await dbContext.Roles
                .Where(r => r.IsDefault && r.Id != role.Id)
                .ToListAsync(cancellationToken);

            foreach (var defaultRole in currentDefaults)
            {
                defaultRole.UnmarkAsDefault();
            }

            role.MarkAsDefault();
        }
        else if (request.IsDefault == false)
        {
            role.UnmarkAsDefault();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuthorizationAuditActions.RoleUpdated,
            "Role",
            role.Id,
            actorResult.Value,
            $"Updated role '{role.Name}'.",
            request.IpAddress,
            cancellationToken);

        return Result.Success();
    }
}
