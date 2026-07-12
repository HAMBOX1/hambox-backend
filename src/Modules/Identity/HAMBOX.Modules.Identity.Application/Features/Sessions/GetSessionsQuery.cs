using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Sessions;

public sealed record GetSessionsQuery : IRequest<Result<IReadOnlyCollection<UserSessionDto>>>;
