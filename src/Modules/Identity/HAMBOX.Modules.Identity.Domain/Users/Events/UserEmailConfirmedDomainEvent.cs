using HAMBOX.Domain.Events;

namespace HAMBOX.Modules.Identity.Domain.Users.Events;

/// <summary>
/// Raised when a user's email address is confirmed.
/// </summary>
/// <param name="UserId">The identifier of the user whose email was confirmed.</param>
public sealed record UserEmailConfirmedDomainEvent(Guid UserId) : BaseDomainEvent;
