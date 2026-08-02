using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

internal sealed class BlockDeviceCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<BlockDeviceCommand, Result>
{
    public async Task<Result> Handle(BlockDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await dbContext.TrustedDevices.FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);
        if (device is null)
        {
            return Result.Failure(IdentityErrors.TrustedDeviceNotFound);
        }

        Guid.TryParse(currentUser.UserId, out var actorUserId);
        device.Block(actorUserId, request.Reason);

        await dbContext.SaveChangesAsync(cancellationToken);

        await securityEventLogger.LogAsync(
            SecurityEventType.DeviceBlock,
            SecurityEventSeverity.High,
            $"Device {device.Fingerprint} was blocked for user {device.UserId}: {request.Reason ?? "no reason given"}",
            actorUserId == Guid.Empty ? null : actorUserId,
            device.UserId,
            request.IpAddress,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
