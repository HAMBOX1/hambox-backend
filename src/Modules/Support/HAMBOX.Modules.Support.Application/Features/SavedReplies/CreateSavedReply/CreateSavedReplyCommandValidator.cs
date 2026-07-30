using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReply;

public sealed class CreateSavedReplyCommandValidator : AbstractValidator<CreateSavedReplyCommand>
{
    public CreateSavedReplyCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty();
    }
}
