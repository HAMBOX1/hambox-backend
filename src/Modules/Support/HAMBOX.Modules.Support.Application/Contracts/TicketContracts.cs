using HAMBOX.Modules.Support.Domain.Tickets;

namespace HAMBOX.Modules.Support.Application.Contracts;

public sealed record TicketTagDto(Guid Id, string Name, string Color);

public sealed record TicketCategoryDto(
    Guid Id, string Name, string Color, string Icon, int SortOrder, bool IsActive, bool IsDefault);

public sealed record TicketPriorityDto(
    Guid Id, string Name, string Color, int Level, int? SlaFirstResponseMinutes, int? SlaResolutionMinutes,
    bool IsActive, bool IsDefault);

public sealed record TicketAttachmentDto(
    Guid Id, string FileName, string ContentType, long FileSizeBytes, string PublicUrl,
    string UploadedByUserId, DateTimeOffset CreatedOnUtc);

public sealed record TicketMessageDto(
    Guid Id,
    string AuthorUserId,
    string AuthorName,
    TicketMessageAuthorRole AuthorRole,
    string Body,
    bool IsInternal,
    bool IsDelivered,
    bool IsRead,
    DateTimeOffset CreatedOnUtc,
    IReadOnlyList<TicketAttachmentDto> Attachments);

public sealed record TicketSummaryDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    TicketStatus Status,
    TicketCategoryDto? Category,
    TicketPriorityDto? Priority,
    string CustomerUserId,
    string CustomerName,
    string CustomerEmail,
    string? AssignedAgentUserId,
    string? AssignedAgentName,
    DateTimeOffset? LastMessageOnUtc,
    TicketMessageAuthorRole? LastMessageByRole,
    IReadOnlyList<TicketTagDto> Tags,
    int? RatingScore,
    DateTimeOffset CreatedOnUtc);

public sealed record TicketStatusHistoryDto(
    TicketStatus FromStatus, TicketStatus ToStatus, string ChangedByUserId, DateTimeOffset CreatedOnUtc);

public sealed record TicketContextOrderDto(
    Guid OrderId, string OrderNumber, decimal TotalAmount, string Status,
    DateTimeOffset CreatedOnUtc, IReadOnlyList<string> ProductNames, bool HasLicenseKeys);

public sealed record TicketCustomerContextDto(
    string CustomerUserId,
    string CustomerName,
    string CustomerEmail,
    string? MembershipPlanName,
    string? MembershipStatus,
    DateTime? MembershipExpiresOnUtc,
    IReadOnlyList<TicketContextOrderDto> RecentOrders,
    string? RelatedOrderNumber,
    string? RelatedProductName,
    string? CustomerCountry,
    string? CustomerBrowser,
    string? CustomerDevice,
    int RecentTicketsCount);

public sealed record TicketDetailDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    TicketStatus Status,
    TicketCategoryDto? Category,
    TicketPriorityDto? Priority,
    string? AssignedAgentUserId,
    string? AssignedAgentName,
    IReadOnlyList<TicketMessageDto> Messages,
    IReadOnlyList<TicketStatusHistoryDto> StatusHistory,
    IReadOnlyList<TicketTagDto> Tags,
    TicketCustomerContextDto Context,
    int? RatingScore,
    string? RatingComment,
    Guid? MergedIntoTicketId,
    string? AiSummary,
    string? AiSentiment,
    DateTimeOffset CreatedOnUtc);
