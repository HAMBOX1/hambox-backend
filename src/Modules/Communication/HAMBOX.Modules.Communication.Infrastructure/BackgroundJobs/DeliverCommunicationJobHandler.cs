using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.BackgroundJobs;
using HAMBOX.Modules.Communication.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Infrastructure.BackgroundJobs;

/// <summary>
/// Executes one queued <see cref="CommunicationMessage"/>: resolves its channel provider, sends, and
/// records the outcome. Thrown exceptions propagate to <c>OperationalJobWorker</c>, which applies the
/// Platform-Settings-driven retry/backoff policy and requeues — no retry logic is duplicated here.
/// </summary>
internal sealed class DeliverCommunicationJobHandler(
    IBackgroundJobSerializer serializer,
    ICommunicationDbContext db,
    ICommunicationProviderRegistry providerRegistry) : BackgroundJobHandlerBase<DeliverCommunicationPayload>(serializer)
{
    public override string JobType => CommunicationJobTypes.Deliver;

    public override async Task HandleAsync(DeliverCommunicationPayload payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var message = await db.CommunicationMessages.FirstOrDefaultAsync(m => m.Id == payload.MessageId, cancellationToken);
        if (message is null)
        {
            return;
        }

        var providerResult = providerRegistry.Resolve(message.Channel);
        if (providerResult.IsFailure)
        {
            message.MarkFailed(providerResult.Error.Description);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        message.MarkSending();
        await db.SaveChangesAsync(cancellationToken);

        var deliveryContext = new CommunicationDeliveryContext(
            message.Id,
            message.UserId,
            message.Category.ToString(),
            message.Subject,
            message.Body,
            message.CorrelationId,
            message.MetadataJson,
            message.RelatedEntityType,
            message.RelatedEntityId);

        var result = await providerResult.Value.SendAsync(deliveryContext, cancellationToken);

        if (result.Success)
        {
            message.MarkDelivered();
        }
        else
        {
            message.MarkFailed(result.ErrorMessage ?? "Delivery failed.");
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? $"Failed to deliver communication message {message.Id}.");
        }
    }
}
