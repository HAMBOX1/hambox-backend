using FluentValidation;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;

namespace HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.UpdateWhatsAppBotConfiguration;

public sealed class UpdateWhatsAppBotConfigurationCommandValidator : AbstractValidator<UpdateWhatsAppBotConfigurationCommand>
{
    private static readonly WhatsAppMenuAction[] RequiredActions = Enum.GetValues<WhatsAppMenuAction>();

    public UpdateWhatsAppBotConfigurationCommandValidator()
    {
        RuleFor(x => x.WelcomeMessageEn).NotEmpty().MaximumLength(500);
        RuleFor(x => x.WelcomeMessageAr).NotEmpty().MaximumLength(500);
        RuleFor(x => x.FallbackMessageEn).NotEmpty().MaximumLength(500);
        RuleFor(x => x.FallbackMessageAr).NotEmpty().MaximumLength(500);

        RuleFor(x => x.Items)
            .NotEmpty()
            .Must(items => items.Count == RequiredActions.Length)
                .WithMessage("Exactly the fixed set of menu actions must be submitted — none added, none removed.")
            .Must(items => items.Select(i => i.Action).Distinct().Count() == items.Count)
                .WithMessage("Each menu action can appear only once.")
            .Must(items => RequiredActions.All(action => items.Any(i => i.Action == action)))
                .WithMessage("An unknown or missing menu action was submitted.")
            .Must(items => items.Any(i => i.IsEnabled))
                .WithMessage("At least one menu item must remain enabled.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Action).IsInEnum();
            item.RuleFor(i => i.LabelEn).NotEmpty().MaximumLength(60);
            item.RuleFor(i => i.LabelAr).NotEmpty().MaximumLength(60);
        });
    }
}
