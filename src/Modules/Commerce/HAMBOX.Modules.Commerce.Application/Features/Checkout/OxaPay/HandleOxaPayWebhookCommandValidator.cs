using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

public sealed class HandleOxaPayWebhookCommandValidator : AbstractValidator<HandleOxaPayWebhookCommand>
{
    public HandleOxaPayWebhookCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.TrackId).MaximumLength(256);
    }
}
