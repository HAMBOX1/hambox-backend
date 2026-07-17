using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedEmails;

public sealed record DeleteBlockedEmailCommand(Guid Id) : IRequest<Result>;
