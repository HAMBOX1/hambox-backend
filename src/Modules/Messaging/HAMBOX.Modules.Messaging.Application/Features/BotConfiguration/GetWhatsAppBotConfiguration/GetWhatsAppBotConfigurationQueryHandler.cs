using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Contracts;
using HAMBOX.Modules.Messaging.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.GetWhatsAppBotConfiguration;

internal sealed class GetWhatsAppBotConfigurationQueryHandler(IMessagingDbContext db)
    : IRequestHandler<GetWhatsAppBotConfigurationQuery, Result<WhatsAppBotConfigurationDto>>
{
    public async Task<Result<WhatsAppBotConfigurationDto>> Handle(
        GetWhatsAppBotConfigurationQuery request, CancellationToken cancellationToken)
    {
        var config = await db.WhatsAppBotConfigurations.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var items = await db.WhatsAppMenuItems.AsNoTracking().OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);

        if (config is null || items.Count == 0)
        {
            // Not seeded yet — hand back the same defaults the engine's own fallback and the DB seeder
            // use, so the admin page always has something sensible to start editing, never a blank form.
            var defaults = WhatsAppBotConfigurationDefaults.Snapshot;
            return Result.Success(new WhatsAppBotConfigurationDto(
                defaults.WelcomeMessageEn, defaults.WelcomeMessageAr, defaults.FallbackMessageEn, defaults.FallbackMessageAr,
                defaults.EnabledItemsInOrder
                    .Select((i, index) => new WhatsAppMenuItemDto(i.Action, IsEnabled: true, index, i.LabelEn, i.LabelAr))
                    .ToList()));
        }

        return Result.Success(new WhatsAppBotConfigurationDto(
            config.WelcomeMessageEn, config.WelcomeMessageAr, config.FallbackMessageEn, config.FallbackMessageAr,
            items.Select(i => new WhatsAppMenuItemDto(i.Action, i.IsEnabled, i.SortOrder, i.LabelEn, i.LabelAr)).ToList()));
    }
}
