using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.BackgroundJobs;
using HAMBOX.Modules.Communication.Application.Contracts;
using HAMBOX.Modules.Communication.Application.Errors;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Application.Features.Messages;

// ─── Queries ───────────────────────────────────────────────────

public sealed record GetCommunicationMessagesQuery(
    int PageNumber,
    int PageSize,
    string? Status,
    string? Channel,
    string? Category,
    string? SearchTerm) : IRequest<Result<PagedResult<CommunicationMessageListItemDto>>>;

internal sealed class GetCommunicationMessagesQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<GetCommunicationMessagesQuery, Result<PagedResult<CommunicationMessageListItemDto>>>
{
    public async Task<Result<PagedResult<CommunicationMessageListItemDto>>> Handle(GetCommunicationMessagesQuery request, CancellationToken cancellationToken)
    {
        var query = db.CommunicationMessages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<CommunicationStatus>(request.Status, true, out var status))
        {
            query = query.Where(m => m.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            query = query.Where(m => m.Channel == request.Channel);
        }

        if (!string.IsNullOrWhiteSpace(request.Category) && Enum.TryParse<CommunicationCategory>(request.Category, true, out var category))
        {
            query = query.Where(m => m.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(m => m.Subject.Contains(term) || m.UserId.Contains(term) || m.TemplateKey.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new CommunicationMessageListItemDto(
                m.Id, m.UserId, m.Channel, m.Category.ToString(), m.Priority.ToString(), m.Status.ToString(),
                m.TemplateKey, m.Subject, m.RetryCount, m.LastError, m.CreatedOnUtc, m.SentOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<CommunicationMessageListItemDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}

public sealed record GetCommunicationMessageQuery(Guid Id) : IRequest<Result<CommunicationMessageDetailDto>>;

internal sealed class GetCommunicationMessageQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<GetCommunicationMessageQuery, Result<CommunicationMessageDetailDto>>
{
    public async Task<Result<CommunicationMessageDetailDto>> Handle(GetCommunicationMessageQuery request, CancellationToken cancellationToken)
    {
        var m = await db.CommunicationMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (m is null)
        {
            return Result.Failure<CommunicationMessageDetailDto>(CommunicationErrors.MessageNotFound);
        }

        return Result.Success(new CommunicationMessageDetailDto(
            m.Id, m.UserId, m.Channel, m.Category.ToString(), m.Priority.ToString(), m.Status.ToString(),
            m.TemplateKey, m.Subject, m.Body, m.RetryCount, m.LastError, m.MetadataJson, m.CorrelationId,
            m.RelatedEntityType, m.RelatedEntityId, m.CreatedOnUtc, m.SentOnUtc, m.FailedOnUtc));
    }
}

public sealed record GetFailedCommunicationDeliveriesQuery(int PageNumber, int PageSize)
    : IRequest<Result<PagedResult<CommunicationMessageListItemDto>>>;

internal sealed class GetFailedCommunicationDeliveriesQueryHandler(ICommunicationDbContext db)
    : IRequestHandler<GetFailedCommunicationDeliveriesQuery, Result<PagedResult<CommunicationMessageListItemDto>>>
{
    public async Task<Result<PagedResult<CommunicationMessageListItemDto>>> Handle(GetFailedCommunicationDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var query = db.CommunicationMessages.AsNoTracking().Where(m => m.Status == CommunicationStatus.Failed);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.FailedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new CommunicationMessageListItemDto(
                m.Id, m.UserId, m.Channel, m.Category.ToString(), m.Priority.ToString(), m.Status.ToString(),
                m.TemplateKey, m.Subject, m.RetryCount, m.LastError, m.CreatedOnUtc, m.SentOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<CommunicationMessageListItemDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}

// ─── Commands ──────────────────────────────────────────────────

public sealed record RetryCommunicationDeliveryCommand(Guid Id) : IRequest<Result>;

internal sealed class RetryCommunicationDeliveryCommandHandler(ICommunicationDbContext db, IBackgroundJobScheduler jobScheduler)
    : IRequestHandler<RetryCommunicationDeliveryCommand, Result>
{
    public async Task<Result> Handle(RetryCommunicationDeliveryCommand request, CancellationToken cancellationToken)
    {
        var message = await db.CommunicationMessages.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
        if (message is null)
        {
            return Result.Failure(CommunicationErrors.MessageNotFound);
        }

        message.MarkRetrying("Manual retry requested.");
        await db.SaveChangesAsync(cancellationToken);

        await jobScheduler.EnqueueAsync(
            CommunicationJobTypes.Deliver,
            new DeliverCommunicationPayload(message.Id),
            new BackgroundJobOptions(
                Queue: message.Channel == CommunicationChannels.Email ? BackgroundJobQueues.Emails : BackgroundJobQueues.Notifications,
                CorrelationId: message.CorrelationId,
                RelatedEntityType: message.RelatedEntityType,
                RelatedEntityId: message.RelatedEntityId),
            cancellationToken);

        return Result.Success();
    }
}
