using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

internal sealed class UnblockUserCommandValidator : AbstractValidator<UnblockUserCommand>
{
    public UnblockUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
