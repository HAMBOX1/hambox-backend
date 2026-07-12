using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.Roles;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Roles;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Roles.CreateRole;

internal sealed class CreateRoleCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IAuthorizationAuditService auditService) : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var actorResult = RoleActorContext.GetActorUserId(currentUserService);
        if (actorResult.IsFailure)
        {
            return Result.Failure<Guid>(actorResult.Error);
        }

        var normalizedName = request.Name.ToUpperInvariant();
        var nameExists = await dbContext.Roles
            .AnyAsync(r => r.NormalizedName == normalizedName, cancellationToken);

        if (nameExists)
        {
            return Result.Failure<Guid>(IdentityErrors.RoleNameExists);
        }

        var role = ApplicationRole.Create(
            request.Name,
            request.Description,
            isDefault: false,
            isSystem: false,
            priorityLevel: request.PriorityLevel ?? 500);

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.RecordAsync(
            AuthorizationAuditActions.RoleCreated,
            "Role",
            role.Id,
            actorResult.Value,
            $"Created role '{role.Name}'.",
            request.IpAddress,
            cancellationToken);

        return Result.Success(role.Id);
    }
}
