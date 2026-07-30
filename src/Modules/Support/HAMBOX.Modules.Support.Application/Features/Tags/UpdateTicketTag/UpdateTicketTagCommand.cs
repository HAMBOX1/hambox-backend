using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tags.UpdateTicketTag;

public sealed record UpdateTicketTagCommand(Guid TagId, string Name, string Color) : IRequest<Result>;
