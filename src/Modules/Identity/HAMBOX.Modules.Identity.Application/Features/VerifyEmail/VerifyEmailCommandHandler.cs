using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.VerifyEmail;

/// <summary>
/// Handler for the <see cref="VerifyEmailCommand"/> command.
/// </summary>
internal sealed class VerifyEmailCommandHandler(IIdentityDbContext dbContext) : IRequestHandler<VerifyEmailCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var token = await dbContext.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.Token == request.Token, cancellationToken);

        if (token is null)
        {
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        if (token.IsUsed)
        {
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        if (token.IsExpired)
        {
            return Result.Failure(IdentityErrors.TokenExpired);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        var customerRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IsDefault, cancellationToken);

        if (customerRole is null)
        {
            return Result.Failure(IdentityErrors.DefaultRoleNotFound);
        }

        token.MarkAsUsed();
        user.ConfirmEmail();
        user.Activate();

        var hasCustomerRole = await dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == customerRole.Id, cancellationToken);

        if (!hasCustomerRole)
        {
            dbContext.UserRoles.Add(UserRole.Create(user.Id, customerRole.Id));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
