using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Application.Features.AuditLog;

public sealed record GetCommunicationAuditLogQuery(int PageNumber, int PageSize)
    : IRequest<Result<PagedResult<CommunicationAuditLogEntryDto>>>;

internal sealed class GetCommunicationAuditLogQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<GetCommunicationAuditLogQuery, Result<PagedResult<CommunicationAuditLogEntryDto>>>
{
    public async Task<Result<PagedResult<CommunicationAuditLogEntryDto>>> Handle(GetCommunicationAuditLogQuery request, CancellationToken cancellationToken)
    {
        var query = db.CommunicationAuditLogs.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new CommunicationAuditLogEntryDto(
                l.Id, l.Action.ToString(), l.ActorUserId, l.EntityType, l.EntityId, l.Details, l.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<CommunicationAuditLogEntryDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
