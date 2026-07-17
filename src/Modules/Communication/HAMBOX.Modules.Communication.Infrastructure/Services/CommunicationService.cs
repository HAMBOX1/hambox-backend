using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Communication;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.BackgroundJobs;
using HAMBOX.Modules.Communication.Application.Errors;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DomainCategory = HAMBOX.Modules.Communication.Domain.Communication.CommunicationCategory;
using DomainPriority = HAMBOX.Modules.Communication.Domain.Communication.CommunicationPriority;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// The one implementation of the BuildingBlocks <c>ICommunicationService</c> contract. Renders the
/// template, checks preferences, records one <see cref="CommunicationMessage"/> row per resolved
/// channel, and enqueues delivery via <see cref="IBackgroundJobScheduler"/> — it never sends anything
/// itself, so the caller's handler always returns immediately.
/// </summary>
internal sealed class CommunicationService(
    ICommunicationDbContext db,
    IIdentityDbContext identityDb,
    ICommunicationTemplateRenderer renderer,
    ICommunicationPreferenceService preferences,
    IPlatformSettingsProvider platformSettings,
    IBackgroundJobScheduler jobScheduler) : ICommunicationService
{
    public async Task<Result> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.UserId, out var userGuid))
        {
            return Result.Success();
        }

        var languageCode = await identityDb.Users
            .Where(u => u.Id == userGuid)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(cancellationToken) ?? "en";

        var candidateChannels = request.ChannelOverride ?? await db.CommunicationProviderConfigs
            .Where(p => p.IsEnabled)
            .Select(p => p.Channel)
            .ToListAsync(cancellationToken);

        var category = (DomainCategory)(int)request.Category;
        var priority = (DomainPriority)(int)request.Priority;

        var metadataJson = string.IsNullOrWhiteSpace(request.ActionUrl)
            ? null
            : JsonSerializer.Serialize(new { actionUrl = request.ActionUrl });

        var anyRendered = false;

        foreach (var channel in candidateChannels)
        {
            if (channel == CommunicationChannels.Email && !await IsEmailChannelGloballyEnabledAsync(cancellationToken))
            {
                continue;
            }

            if (!await preferences.IsChannelEnabledAsync(request.UserId, channel, category, cancellationToken))
            {
                continue;
            }

            var rendered = await renderer.RenderAsync(request.TemplateKey, channel, languageCode, request.Variables, cancellationToken);
            if (rendered.IsFailure)
            {
                continue;
            }

            anyRendered = true;

            var message = CommunicationMessage.Create(
                request.UserId,
                channel,
                category,
                priority,
                request.TemplateKey,
                rendered.Value.Subject,
                rendered.Value.Body,
                request.CorrelationId,
                request.RelatedEntityType,
                request.RelatedEntityId,
                metadataJson);

            db.CommunicationMessages.Add(message);
            await db.SaveChangesAsync(cancellationToken);

            await jobScheduler.EnqueueAsync(
                CommunicationJobTypes.Deliver,
                new DeliverCommunicationPayload(message.Id),
                new BackgroundJobOptions(
                    Queue: channel == CommunicationChannels.Email ? BackgroundJobQueues.Emails : BackgroundJobQueues.Notifications,
                    Priority: (BackgroundJobPriority)(int)priority,
                    CorrelationId: request.CorrelationId,
                    RelatedEntityType: request.RelatedEntityType,
                    RelatedEntityId: request.RelatedEntityId),
                cancellationToken);
        }

        return anyRendered ? Result.Success() : Result.Failure(CommunicationErrors.TemplateNotFound);
    }

    private async Task<bool> IsEmailChannelGloballyEnabledAsync(CancellationToken cancellationToken)
    {
        var settings = await platformSettings.GetAsync<NotificationsSettingsPayload>(
            PlatformSettingsCategoryKeys.Notifications, cancellationToken);
        return settings.EmailEnabled;
    }
}
