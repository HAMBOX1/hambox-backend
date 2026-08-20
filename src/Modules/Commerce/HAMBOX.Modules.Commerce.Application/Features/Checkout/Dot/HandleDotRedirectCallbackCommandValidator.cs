using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

/// <summary>
/// Bounds on browser-supplied query parameters — this is a GET endpoint reachable by an
/// unauthenticated browser redirect, so nothing here is trusted for authorization/business
/// decisions (see the command's own docs), but the lengths still need capping before they reach
/// <c>PaymentAttempt</c>'s fixed-width columns.
/// </summary>
public sealed class HandleDotRedirectCallbackCommandValidator : AbstractValidator<HandleDotRedirectCallbackCommand>
{
    public HandleDotRedirectCallbackCommandValidator()
    {
        RuleFor(x => x.PartnerTxId).MaximumLength(128);
        RuleFor(x => x.DotTxId).MaximumLength(128);
        RuleFor(x => x.ServiceId).MaximumLength(32);
        RuleFor(x => x.Msisdn).MaximumLength(32);
        RuleFor(x => x.ReasonCode).MaximumLength(16);
        RuleFor(x => x.ReasonDesc).MaximumLength(512);
    }
}
