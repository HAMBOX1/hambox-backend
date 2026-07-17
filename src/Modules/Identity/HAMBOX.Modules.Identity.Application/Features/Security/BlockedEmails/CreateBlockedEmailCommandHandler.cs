using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;

internal sealed class CreateBlockedEmailCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<CreateBlockedEmailCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBlockedEmailCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.Pattern.Trim().ToLowerInvariant();
        var exists = await dbContext.BlockedEmails.AnyAsync(b => b.Pattern == normalized, cancellationToken);
        if (exists)
        {
            return Result.Failure<Guid>(IdentityErrors.BlockedEmailAlreadyExists);
        }

        var entry = BlockedEmail.Create(request.Pattern, request.Reason, request.Notes, request.ExpiresOnUtc);
        dbContext.BlockedEmails.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        blocklistService.InvalidateCache();

        Guid.TryParse(currentUser.UserId, out var actorUserId);
        await securityEventLogger.LogAsync(
            SecurityEventType.EmailBlock,
            SecurityEventSeverity.Medium,
            $"Blocked email pattern '{entry.Pattern}': {request.Reason}",
            actorUserId == Guid.Empty ? null : actorUserId,
            ipAddress: request.IpAddress,
            cancellationToken: cancellationToken);

        return Result.Success(entry.Id);
    }
}
