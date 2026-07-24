using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.SaveProductInstructions;

public class SaveProductInstructionsCommandValidator : AbstractValidator<SaveProductInstructionsCommand>
{
    public SaveProductInstructionsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContentHtml).NotNull();
    }
}
