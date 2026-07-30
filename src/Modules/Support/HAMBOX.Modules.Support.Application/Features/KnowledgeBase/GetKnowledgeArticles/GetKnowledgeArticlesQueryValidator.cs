using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticles;

public sealed class GetKnowledgeArticlesQueryValidator : AbstractValidator<GetKnowledgeArticlesQuery>
{
    public GetKnowledgeArticlesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
