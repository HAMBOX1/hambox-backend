using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;

internal sealed class CreateBlockedIpCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<CreateBlockedIpCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBlockedIpCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.CidrOrAddress.Trim();
        var exists = await dbContext.BlockedIps.AnyAsync(b => b.CidrOrAddress == normalized, cancellationToken);
        if (exists)
        {
            return Result.Failure<Guid>(IdentityErrors.BlockedIpAlreadyExists);
        }

        var entry = BlockedIp.Create(request.CidrOrAddress, request.Reason, request.Notes, request.ExpiresOnUtc);
        dbContext.BlockedIps.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        blocklistService.InvalidateCache();

        Guid.TryParse(currentUser.UserId, out var actorUserId);
        await securityEventLogger.LogAsync(
            SecurityEventType.IpBlock,
            SecurityEventSeverity.Medium,
            $"Blocked IP/range '{entry.CidrOrAddress}': {request.Reason}",
            actorUserId == Guid.Empty ? null : actorUserId,
            ipAddress: request.IpAddress,
            cancellationToken: cancellationToken);

        return Result.Success(entry.Id);
    }
}
