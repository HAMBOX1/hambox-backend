using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

internal sealed class UntrustDeviceCommandHandler(IIdentityDbContext dbContext)
    : IRequestHandler<UntrustDeviceCommand, Result>
{
    public async Task<Result> Handle(UntrustDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await dbContext.TrustedDevices.FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);
        if (device is null)
        {
            return Result.Failure(IdentityErrors.TrustedDeviceNotFound);
        }

        device.Untrust();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
