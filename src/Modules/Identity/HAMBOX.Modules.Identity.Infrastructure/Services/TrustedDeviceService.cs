using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class TrustedDeviceService(IIdentityDbContext dbContext) : ITrustedDeviceService
{
    public Task<bool> IsDeviceBlockedAsync(Guid userId, string fingerprint, CancellationToken cancellationToken = default) =>
        dbContext.TrustedDevices.AnyAsync(
            d => d.UserId == userId && d.Fingerprint == fingerprint && d.IsBlocked,
            cancellationToken);

    public async Task<bool> RecordLoginAsync(
        Guid userId,
        string fingerprint,
        LoginContext context,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var device = await dbContext.TrustedDevices.FirstOrDefaultAsync(
            d => d.UserId == userId && d.Fingerprint == fingerprint,
            cancellationToken);

        if (device is null)
        {
            device = TrustedDevice.Create(
                userId, fingerprint, context.BrowserName, context.OsName, context.DeviceType,
                ipAddress, context.CountryCode, context.City);
            dbContext.TrustedDevices.Add(device);
            return true;
        }

        device.RecordLogin(ipAddress, context.CountryCode, context.City);
        return false;
    }
}
