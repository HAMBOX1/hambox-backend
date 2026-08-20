using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Application.Contracts;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.UpdateWhatsAppBotConfiguration;

/// <summary>
/// Always an update-in-place of the same singleton config row and the same seven menu-item rows —
/// never inserts a new menu item, never deletes one (the validator already guarantees the payload names
/// exactly the fixed action set). Writes one audit row per meaningfully-changed field/item rather than
/// one generic "config updated" row, then invalidates <see cref="IWhatsAppBotConfigurationProvider"/>'s
/// cache so the very next inbound WhatsApp message uses the new configuration without a restart.
/// </summary>
internal sealed class UpdateWhatsAppBotConfigurationCommandHandler(
    IMessagingDbContext db, ICurrentUserService currentUser, IWhatsAppBotConfigurationProvider configProvider)
    : IRequestHandler<UpdateWhatsAppBotConfigurationCommand, Result<WhatsAppBotConfigurationDto>>
{
    public async Task<Result<WhatsAppBotConfigurationDto>> Handle(
        UpdateWhatsAppBotConfigurationCommand request, CancellationToken cancellationToken)
    {
        var actorUserId = currentUser.UserId;

        var config = await db.WhatsAppBotConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config is null)
        {
            config = WhatsAppBotConfiguration.CreateDefault(
                request.WelcomeMessageEn, request.WelcomeMessageAr, request.FallbackMessageEn, request.FallbackMessageAr);
            db.WhatsAppBotConfigurations.Add(config);
        }
        else
        {
            if (config.WelcomeMessageEn != request.WelcomeMessageEn || config.WelcomeMessageAr != request.WelcomeMessageAr)
            {
                RecordAudit(WhatsAppBotConfigAuditAction.WelcomeMessageChanged, "WelcomeMessage",
                    $"{config.WelcomeMessageEn} / {config.WelcomeMessageAr}",
                    $"{request.WelcomeMessageEn} / {request.WelcomeMessageAr}", actorUserId);
                config.SetWelcomeMessage(request.WelcomeMessageEn, request.WelcomeMessageAr);
            }

            if (config.FallbackMessageEn != request.FallbackMessageEn || config.FallbackMessageAr != request.FallbackMessageAr)
            {
                RecordAudit(WhatsAppBotConfigAuditAction.FallbackMessageChanged, "FallbackMessage",
                    $"{config.FallbackMessageEn} / {config.FallbackMessageAr}",
                    $"{request.FallbackMessageEn} / {request.FallbackMessageAr}", actorUserId);
                config.SetFallbackMessage(request.FallbackMessageEn, request.FallbackMessageAr);
            }
        }

        var existingItems = await db.WhatsAppMenuItems.ToListAsync(cancellationToken);
        var itemsByAction = existingItems.ToDictionary(i => i.Action);

        var oldOrder = existingItems.OrderBy(i => i.SortOrder).Select(i => i.Action).ToList();
        var newOrder = request.Items.Select(i => i.Action).ToList();

        for (var index = 0; index < request.Items.Count; index++)
        {
            var update = request.Items[index];

            if (!itemsByAction.TryGetValue(update.Action, out var item))
            {
                // Only reachable if the DB was never seeded — the validator already guarantees every
                // fixed action is present in the payload, so this creates the missing row rather than
                // failing an otherwise-valid save.
                item = WhatsAppMenuItem.CreateDefault(update.Action, index, update.LabelEn, update.LabelAr);
                db.WhatsAppMenuItems.Add(item);
                itemsByAction[update.Action] = item;
                continue;
            }

            if (item.IsEnabled != update.IsEnabled)
            {
                RecordAudit(
                    update.IsEnabled ? WhatsAppBotConfigAuditAction.ItemEnabled : WhatsAppBotConfigAuditAction.ItemDisabled,
                    update.Action.ToString(), item.IsEnabled.ToString(), update.IsEnabled.ToString(), actorUserId);
                item.SetEnabled(update.IsEnabled);
            }

            if (item.LabelEn != update.LabelEn || item.LabelAr != update.LabelAr)
            {
                RecordAudit(WhatsAppBotConfigAuditAction.LabelChanged, update.Action.ToString(),
                    $"{item.LabelEn} / {item.LabelAr}", $"{update.LabelEn} / {update.LabelAr}", actorUserId);
                item.SetLabels(update.LabelEn, update.LabelAr);
            }

            item.SetSortOrder(index);
        }

        if (!oldOrder.SequenceEqual(newOrder))
        {
            RecordAudit(WhatsAppBotConfigAuditAction.OrderChanged, "MenuOrder",
                string.Join(',', oldOrder), string.Join(',', newOrder), actorUserId);
        }

        await db.SaveChangesAsync(cancellationToken);
        configProvider.Invalidate();

        var savedItems = await db.WhatsAppMenuItems.AsNoTracking().OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);
        return Result.Success(new WhatsAppBotConfigurationDto(
            config.WelcomeMessageEn, config.WelcomeMessageAr, config.FallbackMessageEn, config.FallbackMessageAr,
            savedItems.Select(i => new WhatsAppMenuItemDto(i.Action, i.IsEnabled, i.SortOrder, i.LabelEn, i.LabelAr)).ToList()));

        void RecordAudit(WhatsAppBotConfigAuditAction action, string target, string? oldValue, string? newValue, string? actor) =>
            db.WhatsAppBotConfigAuditLogs.Add(WhatsAppBotConfigAuditLog.Create(action, target, oldValue, newValue, actor));
    }
}
