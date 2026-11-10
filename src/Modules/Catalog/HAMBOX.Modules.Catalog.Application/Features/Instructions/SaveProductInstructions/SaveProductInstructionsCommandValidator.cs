using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.SaveProductInstructions;

public class SaveProductInstructionsCommandValidator : AbstractValidator<SaveProductInstructionsCommand>
{
    // Generous for rich text with inline images by URL, yet stops a multi-megabyte body from being sanitized and stored.
    public const int MaxContentLength = 200_000;

    public SaveProductInstructionsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContentHtml).NotNull().MaximumLength(MaxContentLength);
    }
}
