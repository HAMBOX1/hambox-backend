using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Identity.Domain.Users.Events;

/// <summary>
/// Raised when a suspended, blocked, or banned user account is restored to active status.
/// </summary>
/// <param name="UserId">The identifier of the unblocked user.</param>
public sealed record UserUnblockedDomainEvent(Guid UserId) : BaseDomainEvent;
