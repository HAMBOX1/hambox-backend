using HAMBOX.Application.Communication;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.CreateTicket;

internal sealed class CreateTicketCommandHandler(
    ISupportDbContext dbContext,
    TicketContextBuilder contextBuilder,
    ICommunicationService communicationService,
    ISupportRealtimeNotifier realtimeNotifier)
    : IRequestHandler<CreateTicketCommand, Result<TicketDetailDto>>
{
    public async Task<Result<TicketDetailDto>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var categoryId = request.CategoryId ?? await dbContext.TicketCategories
            .Where(c => c.IsDefault && c.IsActive)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var priorityId = request.PriorityId ?? await dbContext.TicketPriorities
            .Where(p => p.IsDefault && p.IsActive)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var ticket = Ticket.Create(
            TicketNumberGenerator.Generate(),
            request.Subject,
            request.CustomerUserId,
            categoryId,
            priorityId,
            request.RelatedOrderId,
            request.RelatedProductId,
            request.CustomerCountry,
            request.CustomerBrowser,
            request.CustomerDevice,
            request.CustomerIpAddress);
        ticket.CreatedBy = request.CreatedByUserId;

        dbContext.Tickets.Add(ticket);

        var message = TicketMessage.Create(
            ticket.Id, request.CustomerUserId, TicketMessageAuthorRole.Customer, request.Body, isInternal: false);
        dbContext.TicketMessages.Add(message);

        ticket.RecordCustomerMessage(DateTimeOffset.UtcNow);

        dbContext.TicketAuditLogs.Add(TicketAuditLog.Create(ticket.Id, TicketAuditAction.Created, request.CreatedByUserId));

        await dbContext.SaveChangesAsync(cancellationToken);

        await communicationService.SendAsync(new CommunicationRequest(
            UserId: request.CustomerUserId,
            TemplateKey: SupportTemplateKeys.TicketCreated,
            Category: CommunicationCategory.Support,
            Variables: new Dictionary<string, string>
            {
                ["TicketNumber"] = ticket.TicketNumber,
                ["Subject"] = ticket.Subject,
            },
            RelatedEntityType: "Ticket",
            RelatedEntityId: ticket.Id.ToString(),
            ActionUrl: $"/account/support/tickets/{ticket.Id}"), cancellationToken);

        var category = categoryId is null ? null : await dbContext.TicketCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        var priority = priorityId is null ? null : await dbContext.TicketPriorities.AsNoTracking().FirstOrDefaultAsync(p => p.Id == priorityId, cancellationToken);
        var context = await contextBuilder.BuildAsync(ticket, cancellationToken);
        var messageDto = SupportMapper.ToDto(message, context.CustomerName, []);

        await realtimeNotifier.NotifyTicketCreatedAsync(ticket.Id, ticket.CustomerUserId, cancellationToken);
        await realtimeNotifier.NotifyNewMessageAsync(ticket.Id, messageDto, cancellationToken);

        return Result.Success(new TicketDetailDto(
            ticket.Id, ticket.TicketNumber, ticket.Subject, ticket.Status,
            category is null ? null : SupportMapper.ToDto(category),
            priority is null ? null : SupportMapper.ToDto(priority),
            null, null,
            [messageDto],
            [],
            [],
            context,
            null, null, null, null, null,
            ticket.CreatedOnUtc));
    }
}
