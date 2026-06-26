using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Identity.Domain.Users.Events;

/// <summary>
/// Raised when a user account is suspended.
/// </summary>
/// <param name="UserId">The identifier of the suspended user.</param>
public sealed record UserSuspendedDomainEvent(Guid UserId) : BaseDomainEvent;
