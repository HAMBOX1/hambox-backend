using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.DuplicateFaq;

public sealed record DuplicateFaqCommand(Guid Id) : IRequest<Result<Guid>>;
