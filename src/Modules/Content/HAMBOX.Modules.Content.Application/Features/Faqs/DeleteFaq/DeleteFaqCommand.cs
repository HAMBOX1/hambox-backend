using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.DeleteFaq;

public sealed record DeleteFaqCommand(Guid Id) : IRequest<Result>;
