using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Sessions;

public sealed record RevokeAllSessionsCommand : IRequest<Result>;
