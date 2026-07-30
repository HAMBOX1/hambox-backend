using FluentValidation;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReply;

public sealed class UpdateSavedReplyCommandValidator : AbstractValidator<UpdateSavedReplyCommand>
{
    public UpdateSavedReplyCommandValidator()
    {
        RuleFor(x => x.ReplyId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty();
    }
}
