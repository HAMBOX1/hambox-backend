using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.MergeCart;

public sealed class MergeCartCommandValidator : AbstractValidator<MergeCartCommand>
{
    public MergeCartCommandValidator()
    {
        RuleFor(x => x.GuestSessionId).NotEmpty();
    }
}
