using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tags.GetTicketTags;

public sealed record GetTicketTagsQuery : IRequest<Result<IReadOnlyList<TicketTagDto>>>;
