using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeArticle;

public sealed class CreateKnowledgeArticleCommandValidator : AbstractValidator<CreateKnowledgeArticleCommand>
{
    public CreateKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty();
    }
}
