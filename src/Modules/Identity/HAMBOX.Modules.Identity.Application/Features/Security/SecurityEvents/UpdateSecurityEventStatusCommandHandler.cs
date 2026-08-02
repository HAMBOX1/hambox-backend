using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

internal sealed class UpdateSecurityEventStatusCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateSecurityEventStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateSecurityEventStatusCommand request, CancellationToken cancellationToken)
    {
        var securityEvent = await dbContext.SecurityEventLogs
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (securityEvent is null)
        {
            return Result.Failure(IdentityErrors.SecurityEventNotFound);
        }

        Guid.TryParse(currentUser.UserId, out var actorUserId);

        switch (request.Status)
        {
            case SecurityEventStatus.Acknowledged:
                securityEvent.Acknowledge(actorUserId);
                break;
            case SecurityEventStatus.Dismissed:
                securityEvent.Dismiss(actorUserId, request.Notes);
                break;
            case SecurityEventStatus.Resolved:
                securityEvent.Resolve(actorUserId, request.Notes);
                break;
            default:
                return Result.Failure(IdentityErrors.SecurityEventNotFound);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
