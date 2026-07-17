using FluentValidation;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedUsers;

internal sealed class BlockUserCommandValidator : AbstractValidator<BlockUserCommand>
{
    public BlockUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.ExpiresOnUtc)
            .GreaterThan(DateTimeOffset.UtcNow)
            .When(x => x.ExpiresOnUtc.HasValue)
            .WithMessage("Expiration must be in the future.");
    }
}
