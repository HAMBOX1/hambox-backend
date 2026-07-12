using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.UpdateProfile;

/// <summary>
/// Handler for the <see cref="UpdateProfileCommand"/> command.
/// </summary>
internal sealed class UpdateProfileCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IPermissionResolver permissionResolver) : IRequestHandler<UpdateProfileCommand, Result<UserProfileDto>>
{
    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
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
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.UserNotFound);
        }

        try
        {
            user.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber);

            if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
            {
                user.SetPreferredLanguage(request.PreferredLanguage);
            }

            if (!string.IsNullOrWhiteSpace(request.PreferredCurrency))
            {
                user.SetPreferredCurrency(request.PreferredCurrency);
            }
        }
        catch (ArgumentException)
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.ProfileUpdateFailed);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

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
