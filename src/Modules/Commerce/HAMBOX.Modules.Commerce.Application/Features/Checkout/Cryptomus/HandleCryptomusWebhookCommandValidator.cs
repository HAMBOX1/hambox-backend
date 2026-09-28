using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;

public sealed class HandleCryptomusWebhookCommandValidator : AbstractValidator<HandleCryptomusWebhookCommand>
{
    public HandleCryptomusWebhookCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.Uuid).MaximumLength(256);
    }
}
