using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.LoginHistory;

internal sealed class GetLoginHistoryQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetLoginHistoryQuery, Result<PagedResult<LoginHistoryDto>>>
{
    public async Task<Result<PagedResult<LoginHistoryDto>>> Handle(
        GetLoginHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.LoginHistory.AsNoTracking();

        if (request.UserId.HasValue)
        {
            query = query.Where(h => h.UserId == request.UserId.Value);
        }

        if (request.IsSuccessful.HasValue)
        {
            query = query.Where(h => h.IsSuccessful == request.IsSuccessful.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            query = query.Where(h => h.CountryCode == request.CountryCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Fingerprint))
        {
            query = query.Where(h => h.Fingerprint == request.Fingerprint);
        }

        if (!string.IsNullOrWhiteSpace(request.RiskLevel) &&
            Enum.TryParse<SecurityEventSeverity>(request.RiskLevel, ignoreCase: true, out var riskLevel))
        {
            query = query.Where(h => h.RiskLevel == riskLevel);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(h => h.CreatedOnUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(h => h.CreatedOnUtc <= request.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(h => h.IpAddress.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var records = await query
            .OrderByDescending(h => h.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = records.Select(h => h.UserId).Distinct().ToList();
        var emailsByUserId = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var items = records.Select(h => new LoginHistoryDto(
            h.Id,
            h.UserId,
            emailsByUserId.GetValueOrDefault(h.UserId),
            h.IpAddress,
            h.CountryCode,
            h.City,
            h.BrowserName,
            h.OsName,
            h.DeviceType,
            h.IsSuccessful,
            h.FailureReason,
            h.RiskLevel?.ToString(),
            h.CreatedOnUtc)).ToList();

        return Result.Success(new PagedResult<LoginHistoryDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
