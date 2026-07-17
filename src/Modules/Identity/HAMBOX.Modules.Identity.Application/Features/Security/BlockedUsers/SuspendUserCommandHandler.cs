using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

internal sealed class SuspendUserCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    IRbacAuthorizationService rbacAuthorizationService,
    IUserAuthorizationInvalidationService invalidationService,
    ISecurityEventLogger securityEventLogger,
    IBackgroundJobScheduler jobScheduler) : IRequestHandler<SuspendUserCommand, Result>
{
    public async Task<Result> Handle(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        if (await rbacAuthorizationService.IsOwnerAsync(user.Id, cancellationToken))
        {
            return Result.Failure(IdentityErrors.CannotRestrictOwner);
        }

        Guid.TryParse(currentUser.UserId, out var actorUserId);

        try
        {
            user.Suspend(request.Reason, request.Notes, actorUserId == Guid.Empty ? null : actorUserId);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure(IdentityErrors.InvalidUserStateTransition);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await securityEventLogger.LogAsync(
            SecurityEventType.ManualSuspension,
            SecurityEventSeverity.Medium,
            $"User {user.Email} was suspended: {request.Reason}",
            actorUserId == Guid.Empty ? null : actorUserId,
            user.Id,
            request.IpAddress,
            cancellationToken: cancellationToken);

        await invalidationService.InvalidateUserAsync(user.Id, cancellationToken);
        await jobScheduler.EnqueueAsync(SecurityJobTypes.InvalidateUserSessions, user.Id, cancellationToken: cancellationToken);

        return Result.Success();
    }
}
