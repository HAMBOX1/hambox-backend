using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.Tickets;

namespace HAMBOX.Modules.Support.Application.Services;

internal static class SupportMapper
{
    public static TicketCategoryDto ToDto(TicketCategory category) => new(
        category.Id, category.Name, category.Color, category.Icon, category.SortOrder, category.IsActive, category.IsDefault);

    public static TicketPriorityDto ToDto(TicketPriority priority) => new(
        priority.Id, priority.Name, priority.Color, priority.Level,
        priority.SlaFirstResponseMinutes, priority.SlaResolutionMinutes, priority.IsActive, priority.IsDefault);

    public static TicketTagDto ToDto(TicketTag tag) => new(tag.Id, tag.Name, tag.Color);

    public static TicketAttachmentDto ToDto(TicketAttachment attachment) => new(
        attachment.Id, attachment.FileName, attachment.ContentType, attachment.FileSizeBytes,
        attachment.PublicUrl, attachment.UploadedByUserId, attachment.CreatedOnUtc);

    public static TicketMessageDto ToDto(TicketMessage message, string authorName, IReadOnlyList<TicketAttachmentDto> attachments) => new(
        message.Id, message.AuthorUserId, authorName, message.AuthorRole, message.Body,
        message.IsInternal, message.IsDelivered, message.IsRead, message.CreatedOnUtc, attachments);

    public static TicketStatusHistoryDto ToDto(TicketStatusHistory history) => new(
        history.FromStatus, history.ToStatus, history.ChangedByUserId, history.CreatedOnUtc);

    public static TicketSummaryDto ToSummaryDto(
        Ticket ticket,
        TicketCategory? category,
        TicketPriority? priority,
        string customerName,
        string customerEmail,
        string? agentName,
        IReadOnlyList<TicketTagDto> tags) => new(
        ticket.Id,
        ticket.TicketNumber,
        ticket.Subject,
        ticket.Status,
        category is null ? null : ToDto(category),
        priority is null ? null : ToDto(priority),
        ticket.CustomerUserId,
        customerName,
        customerEmail,
        ticket.AssignedAgentUserId,
        agentName,
        ticket.LastMessageOnUtc,
        ticket.LastMessageByRole,
        tags,
        ticket.RatingScore,
        ticket.CreatedOnUtc);
}
