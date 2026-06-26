using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.AddWishlistItem;

public sealed class AddWishlistItemCommandValidator : AbstractValidator<AddWishlistItemCommand>
{
    public AddWishlistItemCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
