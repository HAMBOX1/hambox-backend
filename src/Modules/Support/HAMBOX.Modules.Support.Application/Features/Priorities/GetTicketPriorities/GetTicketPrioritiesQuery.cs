using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.GetTicketPriorities;

public sealed record GetTicketPrioritiesQuery : IRequest<Result<IReadOnlyList<TicketPriorityDto>>>;
