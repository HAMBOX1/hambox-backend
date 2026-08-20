using HAMBOX.Modules.Messaging.Domain.BotConfiguration;

namespace HAMBOX.Modules.Messaging.Application.Contracts;

public sealed record WhatsAppMenuItemDto(WhatsAppMenuAction Action, bool IsEnabled, int SortOrder, string LabelEn, string LabelAr);

public sealed record WhatsAppBotConfigurationDto(
    string WelcomeMessageEn,
    string WelcomeMessageAr,
    string FallbackMessageEn,
    string FallbackMessageAr,
    IReadOnlyList<WhatsAppMenuItemDto> Items);
