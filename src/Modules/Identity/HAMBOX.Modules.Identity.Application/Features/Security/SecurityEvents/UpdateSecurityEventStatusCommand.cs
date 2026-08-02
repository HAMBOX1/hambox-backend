using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

/// <summary>
/// Moves a security event through its investigation workflow (Acknowledge/Dismiss/Resolve —
/// <see cref="SecurityEventStatus.Open"/> is the only status not settable here, it's the default).
/// </summary>
public sealed record UpdateSecurityEventStatusCommand(
    Guid EventId,
    SecurityEventStatus Status,
    string? Notes) : IRequest<Result>;
