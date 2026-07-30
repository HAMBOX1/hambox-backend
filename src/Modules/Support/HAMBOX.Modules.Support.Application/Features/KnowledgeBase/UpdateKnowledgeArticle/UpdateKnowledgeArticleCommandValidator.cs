using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeArticle;

public sealed class UpdateKnowledgeArticleCommandValidator : AbstractValidator<UpdateKnowledgeArticleCommand>
{
    public UpdateKnowledgeArticleCommandValidator()
    {
        RuleFor(x => x.ArticleId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty();
    }
}
