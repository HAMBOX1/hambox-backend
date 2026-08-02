using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

internal sealed class GetTrustedDevicesQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetTrustedDevicesQuery, Result<PagedResult<TrustedDeviceDto>>>
{
    public async Task<Result<PagedResult<TrustedDeviceDto>>> Handle(
        GetTrustedDevicesQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TrustedDevices.AsNoTracking();

        if (request.UserId.HasValue)
        {
            query = query.Where(d => d.UserId == request.UserId.Value);
        }

        if (request.IsTrusted.HasValue)
        {
            query = query.Where(d => d.IsTrusted == request.IsTrusted.Value);
        }

        if (request.IsBlocked.HasValue)
        {
            query = query.Where(d => d.IsBlocked == request.IsBlocked.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(d =>
                d.LastIpAddress.Contains(term) ||
                (d.BrowserName != null && d.BrowserName.Contains(term)) ||
                (d.OsName != null && d.OsName.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var devices = await query
            .OrderByDescending(d => d.LastSeenUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = devices.Select(d => d.UserId).Distinct().ToList();
        var emailsByUserId = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var items = devices.Select(d => new TrustedDeviceDto(
            d.Id,
            d.UserId,
            emailsByUserId.GetValueOrDefault(d.UserId),
            $"{d.BrowserName ?? "Unknown browser"} on {d.OsName ?? "Unknown OS"}",
            d.BrowserName,
            d.OsName,
            d.DeviceType,
            d.FirstSeenUtc,
            d.LastSeenUtc,
            d.LastIpAddress,
            d.LastCountryCode,
            d.LastCity,
            d.LoginCount,
            d.IsTrusted,
            d.TrustedOnUtc,
            d.IsBlocked,
            d.BlockedOnUtc,
            d.BlockReason)).ToList();

        return Result.Success(new PagedResult<TrustedDeviceDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
