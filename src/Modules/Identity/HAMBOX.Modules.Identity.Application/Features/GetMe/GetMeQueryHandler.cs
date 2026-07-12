using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.GetMe;

internal sealed class GetMeQueryHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionResolver permissionResolver) : IRequestHandler<GetMeQuery, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.AuthenticationRequired);
        }

        if (!Guid.TryParse(currentUserService.UserId, out var userId))
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.AuthenticationRequired);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.UserNotFound);
        }

        var roles = await (
            from ur in dbContext.UserRoles
            join r in dbContext.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId
            select r.Name
        ).ToListAsync(cancellationToken);

        var permissions = await permissionResolver.GetPermissionsAsync(userId, cancellationToken);

        var profile = new UserProfileDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.AvatarUrl,
            user.EmailConfirmed,
            user.Status.ToString(),
            user.PreferredLanguage,
            user.PreferredCurrency,
            user.CreatedOnUtc,
            roles,
            permissions.ToArray());

        return Result.Success(profile);
    }
}
