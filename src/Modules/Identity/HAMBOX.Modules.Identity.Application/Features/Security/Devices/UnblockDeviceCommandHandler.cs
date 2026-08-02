using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

internal sealed class UnblockDeviceCommandHandler(IIdentityDbContext dbContext)
    : IRequestHandler<UnblockDeviceCommand, Result>
{
    public async Task<Result> Handle(UnblockDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await dbContext.TrustedDevices.FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);
        if (device is null)
        {
            return Result.Failure(IdentityErrors.TrustedDeviceNotFound);
        }

        device.Unblock();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
