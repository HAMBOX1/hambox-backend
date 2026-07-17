using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;

public sealed record DeleteBlockedIpCommand(Guid Id) : IRequest<Result>;
