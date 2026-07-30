using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tags.DeleteTicketTag;

public sealed record DeleteTicketTagCommand(Guid TagId) : IRequest<Result>;
