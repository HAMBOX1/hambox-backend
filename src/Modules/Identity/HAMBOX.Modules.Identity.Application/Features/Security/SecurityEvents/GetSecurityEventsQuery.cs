using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

public sealed record GetSecurityEventsQuery(
    int PageNumber,
    int PageSize,
    string? EventType,
    string? Severity,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? SearchTerm,
    string? Status = null,
    string? MinSeverity = null) : IRequest<Result<PagedResult<SecurityEventDto>>>;
