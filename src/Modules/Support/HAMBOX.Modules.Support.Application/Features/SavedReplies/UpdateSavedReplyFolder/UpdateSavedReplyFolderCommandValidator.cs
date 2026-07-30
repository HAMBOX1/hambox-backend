using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReplyFolder;

public sealed class UpdateSavedReplyFolderCommandValidator : AbstractValidator<UpdateSavedReplyFolderCommand>
{
    public UpdateSavedReplyFolderCommandValidator()
    {
        RuleFor(x => x.FolderId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
