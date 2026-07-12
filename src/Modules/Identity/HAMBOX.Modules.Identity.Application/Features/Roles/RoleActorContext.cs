using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Identity.Application.Features.Roles;

internal static class RoleActorContext
{
    internal static Result<Guid> GetActorUserId(ICurrentUserService currentUserService)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<Guid>(IdentityErrors.AuthenticationRequired);
        }

        if (!Guid.TryParse(currentUserService.UserId, out var userId))
        {
            return Result.Failure<Guid>(IdentityErrors.AuthenticationRequired);
        }

        return Result.Success(userId);
    }
}
