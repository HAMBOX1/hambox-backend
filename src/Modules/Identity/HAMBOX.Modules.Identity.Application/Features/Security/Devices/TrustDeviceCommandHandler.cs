using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

internal sealed class TrustDeviceCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<TrustDeviceCommand, Result>
{
    public async Task<Result> Handle(TrustDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await dbContext.TrustedDevices.FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);
        if (device is null)
        {
            return Result.Failure(IdentityErrors.TrustedDeviceNotFound);
        }

        Guid.TryParse(currentUser.UserId, out var actorUserId);
        device.Trust(actorUserId);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
