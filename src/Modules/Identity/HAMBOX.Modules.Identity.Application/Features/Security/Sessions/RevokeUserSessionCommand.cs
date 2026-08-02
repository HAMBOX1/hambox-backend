using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Sessions;

/// <summary>
/// Revokes a single session (and its linked refresh token) for a target user.
/// </summary>
public sealed record RevokeUserSessionCommand(Guid UserId, Guid SessionId) : IRequest<Result>;
