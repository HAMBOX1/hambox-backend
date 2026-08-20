using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

public sealed class HandleDotNotificationCommandValidator : AbstractValidator<HandleDotNotificationCommand>
{
    public HandleDotNotificationCommandValidator()
    {
        RuleFor(x => x.PartnerTransId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ResultCode).NotEmpty().MaximumLength(16);
        RuleFor(x => x.DotTransId).MaximumLength(256);
        RuleFor(x => x.ServiceId).MaximumLength(64);
        RuleFor(x => x.Msisdn).MaximumLength(32);
        RuleFor(x => x.Amount).MaximumLength(32);
        RuleFor(x => x.ResultDesc).MaximumLength(512);
    }
}
