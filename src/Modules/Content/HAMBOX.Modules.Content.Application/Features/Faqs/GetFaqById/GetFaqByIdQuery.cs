using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqById;

public sealed record GetFaqByIdQuery(Guid Id) : IRequest<Result<FaqDto>>;
