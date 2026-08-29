using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.OtpEvents;

/// <summary>
/// Read model for the customer OTP/verification-token audit trail — the admin/support surface that
/// answers "what OTP action happened to this customer, when, for what reason, and what was the
/// result" without ever exposing the token or code value itself.
/// </summary>
internal sealed class GetCustomerOtpEventsQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetCustomerOtpEventsQuery, Result<PagedResult<CustomerOtpEventDto>>>
{
    public async Task<Result<PagedResult<CustomerOtpEventDto>>> Handle(
        GetCustomerOtpEventsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CustomerOtpAuditLogs.AsNoTracking();

        if (request.UserId.HasValue)
        {
            query = query.Where(e => e.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Purpose) &&
            Enum.TryParse<CustomerOtpPurpose>(request.Purpose, ignoreCase: true, out var purpose))
        {
            query = query.Where(e => e.Purpose == purpose);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<CustomerOtpEventStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(e => e.Status == status);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(e => e.OccurredOnUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(e => e.OccurredOnUtc <= request.ToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderByDescending(e => e.OccurredOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = events
            .Where(e => e.UserId.HasValue)
            .Select(e => e.UserId!.Value)
            .Distinct()
            .ToList();

        var emailsByUserId = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var items = events.Select(e => new CustomerOtpEventDto(
            e.Id,
            e.Purpose.ToString(),
            e.Status.ToString(),
            e.UserId,
            e.UserId.HasValue ? emailsByUserId.GetValueOrDefault(e.UserId.Value) : null,
            e.IssuedOnUtc,
            e.ExpiresOnUtc,
            e.UsedOnUtc,
            e.IpAddress,
            e.UserAgent,
            e.CorrelationId,
            e.EmailDeliveryStatus.ToString(),
            e.Description,
            e.OccurredOnUtc)).ToList();

        return Result.Success(new PagedResult<CustomerOtpEventDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
