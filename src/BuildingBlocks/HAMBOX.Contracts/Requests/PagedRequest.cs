namespace HAMBOX.Contracts.Requests;

/// <summary>
/// Represents a paged request contract.
/// </summary>
/// <param name="PageNumber">The requested page number.</param>
/// <param name="PageSize">The requested page size.</param>
public sealed record PagedRequest(int PageNumber = 1, int PageSize = 20);
