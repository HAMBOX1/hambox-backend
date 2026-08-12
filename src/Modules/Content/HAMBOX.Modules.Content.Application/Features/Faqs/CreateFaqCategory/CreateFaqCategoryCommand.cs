using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaqCategory;

public sealed record CreateFaqCategoryCommand(string NameEn, string? NameAr) : IRequest<Result<FaqCategoryDto>>;
