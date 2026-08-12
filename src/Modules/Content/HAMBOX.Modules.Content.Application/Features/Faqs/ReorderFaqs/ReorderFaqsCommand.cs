using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.ReorderFaqs;

public sealed record ReorderFaqsCommand(IReadOnlyList<FaqReorderEntry> Entries) : IRequest<Result>;
