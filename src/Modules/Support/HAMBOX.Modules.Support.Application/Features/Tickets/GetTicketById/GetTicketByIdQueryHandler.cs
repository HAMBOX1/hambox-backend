using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.GetTicketById;

internal sealed class GetTicketByIdQueryHandler(
    ISupportDbContext dbContext, IIdentityDbContext identityDb, TicketContextBuilder contextBuilder)
    : IRequestHandler<GetTicketByIdQuery, Result<TicketDetailDto>>
{
    public async Task<Result<TicketDetailDto>> Handle(GetTicketByIdQuery request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
        {
            return Result.Failure<TicketDetailDto>(SupportErrors.TicketNotFound);
        }

        var isCustomerRequest = request.RequestingCustomerUserId is not null;
        if (isCustomerRequest && ticket.CustomerUserId != request.RequestingCustomerUserId)
        {
            return Result.Failure<TicketDetailDto>(SupportErrors.NotYourTicket);
        }

        var messagesQuery = dbContext.TicketMessages.AsNoTracking().Where(m => m.TicketId == ticket.Id);
        if (isCustomerRequest)
        {
            messagesQuery = messagesQuery.Where(m => !m.IsInternal);
        }

        var messages = await messagesQuery.OrderBy(m => m.CreatedOnUtc).ToListAsync(cancellationToken);
        var attachments = await dbContext.TicketAttachments.AsNoTracking()
            .Where(a => a.TicketId == ticket.Id).ToListAsync(cancellationToken);

        var category = ticket.CategoryId is null ? null : await dbContext.TicketCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == ticket.CategoryId, cancellationToken);
        var priority = ticket.PriorityId is null ? null : await dbContext.TicketPriorities.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == ticket.PriorityId, cancellationToken);

        var tagAssignments = await dbContext.TicketTagAssignments.AsNoTracking()
            .Where(a => a.TicketId == ticket.Id).ToListAsync(cancellationToken);
        var tagIds = tagAssignments.Select(a => a.TagId).ToList();
        var tagEntities = await dbContext.TicketTags.AsNoTracking()
            .Where(t => tagIds.Contains(t.Id)).ToListAsync(cancellationToken);
        var tags = tagEntities.Select(SupportMapper.ToDto).ToList();

        var statusHistory = await dbContext.TicketStatusHistories.AsNoTracking()
            .Where(h => h.TicketId == ticket.Id).OrderBy(h => h.CreatedOnUtc)
            .Select(h => new { h.FromStatus, h.ToStatus, h.ChangedByUserId, h.CreatedOnUtc })
            .ToListAsync(cancellationToken);

        var userIds = messages.Select(m => m.AuthorUserId).Append(ticket.AssignedAgentUserId ?? string.Empty);
        var users = await UserDisplayResolver.ResolveAsync(identityDb, userIds, cancellationToken);

        var messageDtos = messages.Select(m =>
        {
            var authorName = users.TryGetValue(m.AuthorUserId, out var author) ? author.Name : "Unknown";
            var messageAttachments = attachments.Where(a => a.MessageId == m.Id).Select(SupportMapper.ToDto).ToList();
            return SupportMapper.ToDto(m, authorName, messageAttachments);
        }).ToList();

        var agentName = ticket.AssignedAgentUserId is not null && users.TryGetValue(ticket.AssignedAgentUserId, out var agent)
            ? agent.Name
            : null;

        var context = await contextBuilder.BuildAsync(ticket, cancellationToken);

        return Result.Success(new TicketDetailDto(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Subject,
            ticket.Status,
            category is null ? null : SupportMapper.ToDto(category),
            priority is null ? null : SupportMapper.ToDto(priority),
            ticket.AssignedAgentUserId,
            agentName,
            messageDtos,
            statusHistory.Select(h => new TicketStatusHistoryDto(h.FromStatus, h.ToStatus, h.ChangedByUserId, h.CreatedOnUtc)).ToList(),
            tags,
            context,
            ticket.RatingScore,
            ticket.RatingComment,
            ticket.MergedIntoTicketId,
            isCustomerRequest ? null : ticket.AiSummary,
            isCustomerRequest ? null : ticket.AiSentiment,
            ticket.CreatedOnUtc));
    }
}
