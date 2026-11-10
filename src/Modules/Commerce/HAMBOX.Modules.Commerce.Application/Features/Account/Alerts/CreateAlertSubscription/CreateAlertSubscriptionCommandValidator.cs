using FluentValidation;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.CreateAlertSubscription;

/// <summary>An alert type that is not a defined value (e.g. 99) must not be stored as a subscription.</summary>
public sealed class CreateAlertSubscriptionCommandValidator : AbstractValidator<CreateAlertSubscriptionCommand>
{
    public CreateAlertSubscriptionCommandValidator() => RuleFor(x => x.AlertType).IsInEnum();
}
