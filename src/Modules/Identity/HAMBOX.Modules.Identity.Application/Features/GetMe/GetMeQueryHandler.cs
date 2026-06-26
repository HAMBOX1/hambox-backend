using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.GetMe;

/// <summary>
/// Handler for the <see cref="GetMeQuery"/> query.
/// </summary>
internal sealed class GetMeQueryHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetMeQuery, Result<UserProfileDto>>
{
    /// <inheritdoc />
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

        var profile = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfileDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.AvatarUrl,
                u.EmailConfirmed,
                u.Status.ToString(),
                u.PreferredLanguage,
                u.PreferredCurrency,
                u.CreatedOnUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return Result.Failure<UserProfileDto>(IdentityErrors.UserNotFound);
        }

        return Result.Success(profile);
    }
}
