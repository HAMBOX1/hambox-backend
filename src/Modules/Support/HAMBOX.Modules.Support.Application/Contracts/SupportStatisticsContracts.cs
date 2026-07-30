namespace HAMBOX.Modules.Support.Application.Contracts;

public sealed record TicketVolumePointDto(DateOnly Date, int Count);

public sealed record AgentWorkloadDto(string AgentUserId, string AgentName, int OpenAssignedCount);

public sealed record CategoryBreakdownDto(Guid CategoryId, string Name, int Count);

public sealed record PriorityBreakdownDto(Guid PriorityId, string Name, int Count);

public sealed record SupportStatisticsDto(
    int TotalTickets,
    int OpenTickets,
    int WaitingCustomerTickets,
    int WaitingAgentTickets,
    int ResolvedTickets,
    int ClosedTickets,
    IReadOnlyList<TicketVolumePointDto> TicketsByDay,
    double? AverageFirstResponseMinutes,
    double? AverageResolutionMinutes,
    IReadOnlyList<AgentWorkloadDto> AgentWorkload,
    IReadOnlyList<CategoryBreakdownDto> CategoryBreakdown,
    IReadOnlyList<PriorityBreakdownDto> PriorityBreakdown,
    double? AverageRating,
    int RatingCount);
