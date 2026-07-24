using FluentValidation;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.UpdateCollection;

public class UpdateCollectionCommandValidator : AbstractValidator<UpdateCollectionCommand>
{
    public UpdateCollectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Color).MaximumLength(20);
        RuleFor(x => x.Icon).MaximumLength(50);
    }
}
