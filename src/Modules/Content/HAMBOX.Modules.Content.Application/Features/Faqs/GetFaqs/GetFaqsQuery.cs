using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqs;

public sealed record GetFaqsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    FaqScope? Scope = null,
    Guid? CategoryId = null,
    bool? IsPublished = null) : IRequest<Result<PagedResult<FaqDto>>>;
