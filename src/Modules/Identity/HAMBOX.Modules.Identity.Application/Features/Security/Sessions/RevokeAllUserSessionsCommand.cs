using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Sessions;

/// <summary>
/// Revokes every active session (and refresh token) for a target user — the admin-facing
/// counterpart to the self-service <see cref="Application.Features.Sessions.RevokeAllSessionsCommand"/>.
/// </summary>
public sealed record RevokeAllUserSessionsCommand(Guid UserId) : IRequest<Result>;
