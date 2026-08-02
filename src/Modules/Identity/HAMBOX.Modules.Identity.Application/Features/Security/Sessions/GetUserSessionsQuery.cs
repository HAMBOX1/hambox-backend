using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Sessions;

/// <summary>
/// Lists a specific user's sessions for admin investigation/management — the admin-facing
/// counterpart to the self-service <see cref="Application.Features.Sessions.GetSessionsQuery"/>.
/// </summary>
public sealed record GetUserSessionsQuery(Guid UserId) : IRequest<Result<IReadOnlyCollection<UserSessionDto>>>;
