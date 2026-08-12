using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.UpdateFaq;

public sealed record UpdateFaqCommand(
    Guid Id,
    string QuestionEn,
    string? QuestionAr,
    string AnswerEn,
    string? AnswerAr,
    Guid CategoryId,
    FaqScope Scope,
    Guid? TargetId) : IRequest<Result>;
