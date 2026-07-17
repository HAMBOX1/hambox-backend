using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

internal sealed class UnblockUserCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    IUserAuthorizationInvalidationService invalidationService,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<UnblockUserCommand, Result>
{
    public async Task<Result> Handle(UnblockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        Guid.TryParse(currentUser.UserId, out var actorUserId);

        try
        {
            user.Unblock(actorUserId == Guid.Empty ? null : actorUserId);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure(IdentityErrors.UserNotRestricted);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await securityEventLogger.LogAsync(
            SecurityEventType.AdminUnlock,
            SecurityEventSeverity.Low,
            $"User {user.Email} was restored to active status.",
            actorUserId == Guid.Empty ? null : actorUserId,
            user.Id,
            request.IpAddress,
            cancellationToken: cancellationToken);

        await invalidationService.InvalidateUserAsync(user.Id, cancellationToken);

        return Result.Success();
    }
}
