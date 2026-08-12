using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaq;

public sealed record CreateFaqCommand(
    string QuestionEn,
    string? QuestionAr,
    string AnswerEn,
    string? AnswerAr,
    Guid CategoryId,
    FaqScope Scope,
    Guid? TargetId,
    int SortOrder = 0) : IRequest<Result<Guid>>;
