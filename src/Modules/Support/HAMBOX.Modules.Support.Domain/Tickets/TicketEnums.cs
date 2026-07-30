namespace HAMBOX.Modules.Support.Domain.Tickets;

public enum TicketStatus
{
    Open = 0,
    WaitingCustomer = 1,
    WaitingAgent = 2,
    Resolved = 3,
    Closed = 4,
}

public enum TicketMessageAuthorRole
{
    Customer = 0,
    Agent = 1,
    System = 2,
}

public enum TicketAuditAction
{
    Created = 0,
    Assigned = 1,
    Transferred = 2,
    StatusChanged = 3,
    Replied = 4,
    InternalNoteAdded = 5,
    AttachmentAdded = 6,
    Merged = 7,
    Rated = 8,
    Deleted = 9,
    Reopened = 10,
    TagAdded = 11,
    TagRemoved = 12,
    CategoryChanged = 13,
    PriorityChanged = 14,
}
