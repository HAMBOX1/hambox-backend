using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tags.CreateTicketTag;

public sealed record CreateTicketTagCommand(string Name, string Color) : IRequest<Result<TicketTagDto>>;
