using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartItem;

public sealed class RemoveCartItemCommandValidator : AbstractValidator<RemoveCartItemCommand>
{
    public RemoveCartItemCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
