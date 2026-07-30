using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReplyFolder;

public sealed class CreateSavedReplyFolderCommandValidator : AbstractValidator<CreateSavedReplyFolderCommand>
{
    public CreateSavedReplyFolderCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
