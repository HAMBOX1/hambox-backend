using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeCategory;

public sealed class CreateKnowledgeCategoryCommandValidator : AbstractValidator<CreateKnowledgeCategoryCommand>
{
    public CreateKnowledgeCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
