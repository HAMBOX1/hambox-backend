using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeCategory;

public sealed class UpdateKnowledgeCategoryCommandValidator : AbstractValidator<UpdateKnowledgeCategoryCommand>
{
    public UpdateKnowledgeCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
