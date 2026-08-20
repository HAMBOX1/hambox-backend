using HAMBOX.Modules.Messaging.Application.Contracts;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.UpdateWhatsAppBotConfiguration;

/// <summary>One row from the admin's menu-item list. <see cref="WhatsAppMenuItemUpdate.Action"/> must be
/// one of the fixed <see cref="WhatsAppMenuAction"/> values — there is no free-text action field, so a
/// client can never introduce a new one. <c>SortOrder</c> is not a field here: it is the item's position
/// in <see cref="UpdateWhatsAppBotConfigurationCommand.Items"/>, so the order is exactly what was
/// submitted, never a separately-editable (and therefore possibly inconsistent) number.</summary>
public sealed record WhatsAppMenuItemUpdate(WhatsAppMenuAction Action, bool IsEnabled, string LabelEn, string LabelAr);

public sealed record UpdateWhatsAppBotConfigurationCommand(
    string WelcomeMessageEn,
    string WelcomeMessageAr,
    string FallbackMessageEn,
    string FallbackMessageAr,
    IReadOnlyList<WhatsAppMenuItemUpdate> Items) : IRequest<Result<WhatsAppBotConfigurationDto>>;
