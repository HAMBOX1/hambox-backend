using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

/// <summary>
/// Aggregate root for a support ticket. Internal notes are not a separate entity — a
/// <see cref="TicketMessage"/> with <see cref="TicketMessage.IsInternal"/> set is the same
/// conversation thread, filtered by permission at the query layer, matching the shape already
/// built into the frontend's ticket model.
/// </summary>
public sealed class Ticket : AggregateRoot, IAuditable, ISoftDeletable
{
    private Ticket()
    {
    }

    private Ticket(Guid id, string ticketNumber, string subject, string customerUserId)
        : base(id)
    {
        TicketNumber = ticketNumber;
        Subject = subject;
        CustomerUserId = customerUserId;
        Status = TicketStatus.Open;
    }

    public string TicketNumber { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string CustomerUserId { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public Guid? PriorityId { get; private set; }
    public TicketStatus Status { get; private set; }
    public string? AssignedAgentUserId { get; private set; }
    public DateTimeOffset? AssignedOnUtc { get; private set; }
    public DateTimeOffset? FirstResponseOnUtc { get; private set; }
    public DateTimeOffset? ResolvedOnUtc { get; private set; }
    public DateTimeOffset? ClosedOnUtc { get; private set; }
    public int ReopenedCount { get; private set; }

    public int? RatingScore { get; private set; }
    public string? RatingComment { get; private set; }
    public DateTimeOffset? RatedOnUtc { get; private set; }

    public Guid? MergedIntoTicketId { get; private set; }

    public Guid? RelatedOrderId { get; private set; }
    public Guid? RelatedProductId { get; private set; }

    public string? CustomerCountry { get; private set; }
    public string? CustomerBrowser { get; private set; }
    public string? CustomerDevice { get; private set; }
    public string? CustomerIpAddress { get; private set; }

    public DateTimeOffset? LastMessageOnUtc { get; private set; }
    public TicketMessageAuthorRole? LastMessageByRole { get; private set; }
    public DateTimeOffset? LastCustomerMessageOnUtc { get; private set; }
    public DateTimeOffset? LastAgentMessageOnUtc { get; private set; }

    // AI extension points (§ AI preparation) — populated only if ISupportAiAssistant is ever
    // given a real implementation; NullSupportAiAssistant leaves these untouched.
    public string? AiSummary { get; private set; }
    public string? AiSentiment { get; private set; }
    public Guid? AiSuggestedCategoryId { get; private set; }
    public Guid? AiSuggestedPriorityId { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static Ticket Create(
        string ticketNumber,
        string subject,
        string customerUserId,
        Guid? categoryId,
        Guid? priorityId,
        Guid? relatedOrderId,
        Guid? relatedProductId,
        string? customerCountry,
        string? customerBrowser,
        string? customerDevice,
        string? customerIpAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerUserId);

        return new Ticket(Guid.NewGuid(), ticketNumber, subject.Trim(), customerUserId)
        {
            CategoryId = categoryId,
            PriorityId = priorityId,
            RelatedOrderId = relatedOrderId,
            RelatedProductId = relatedProductId,
            CustomerCountry = customerCountry,
            CustomerBrowser = customerBrowser,
            CustomerDevice = customerDevice,
            CustomerIpAddress = customerIpAddress,
        };
    }

    public void AssignTo(string agentUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentUserId);
        AssignedAgentUserId = agentUserId;
        AssignedOnUtc = DateTimeOffset.UtcNow;
        if (Status == TicketStatus.Open)
        {
            Status = TicketStatus.WaitingAgent;
        }
    }

    public void Transfer(string newAgentUserId) => AssignTo(newAgentUserId);

    public void ChangeStatus(TicketStatus status)
    {
        Status = status;
        switch (status)
        {
            case TicketStatus.Resolved:
                ResolvedOnUtc = DateTimeOffset.UtcNow;
                break;
            case TicketStatus.Closed:
                ClosedOnUtc = DateTimeOffset.UtcNow;
                break;
        }
    }

    public void Reopen()
    {
        Status = TicketStatus.WaitingAgent;
        ReopenedCount++;
        ResolvedOnUtc = null;
        ClosedOnUtc = null;
    }

    public void MergeInto(Guid targetTicketId)
    {
        MergedIntoTicketId = targetTicketId;
        Status = TicketStatus.Closed;
        ClosedOnUtc = DateTimeOffset.UtcNow;
    }

    public void Rate(int score, string? comment)
    {
        if (score is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Rating score must be between 1 and 5.");
        }

        RatingScore = score;
        RatingComment = comment;
        RatedOnUtc = DateTimeOffset.UtcNow;
    }

    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    public void SetPriority(Guid? priorityId) => PriorityId = priorityId;

    public void RecordCustomerMessage(DateTimeOffset atUtc)
    {
        LastMessageOnUtc = atUtc;
        LastMessageByRole = TicketMessageAuthorRole.Customer;
        LastCustomerMessageOnUtc = atUtc;
        if (Status is TicketStatus.Resolved or TicketStatus.WaitingAgent)
        {
            Status = TicketStatus.WaitingAgent;
        }
        else if (Status == TicketStatus.Open)
        {
            Status = TicketStatus.WaitingAgent;
        }
    }

    public void RecordAgentMessage(DateTimeOffset atUtc, bool isInternal)
    {
        if (isInternal)
        {
            return;
        }

        LastMessageOnUtc = atUtc;
        LastMessageByRole = TicketMessageAuthorRole.Agent;
        LastAgentMessageOnUtc = atUtc;
        FirstResponseOnUtc ??= atUtc;
        Status = TicketStatus.WaitingCustomer;
    }

    public void SetAiSuggestions(string? summary, string? sentiment, Guid? suggestedCategoryId, Guid? suggestedPriorityId)
    {
        AiSummary = summary;
        AiSentiment = sentiment;
        AiSuggestedCategoryId = suggestedCategoryId;
        AiSuggestedPriorityId = suggestedPriorityId;
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }
}
