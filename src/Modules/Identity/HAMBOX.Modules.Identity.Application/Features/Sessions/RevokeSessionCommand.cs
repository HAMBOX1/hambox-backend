using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Sessions;

/// <summary>
/// Revokes one of the caller's own sessions (and its linked refresh token) — the self-service
/// counterpart to the admin-facing <see cref="Application.Features.Security.Sessions.RevokeUserSessionCommand"/>.
/// </summary>
public sealed record RevokeSessionCommand(Guid SessionId) : IRequest<Result>;
