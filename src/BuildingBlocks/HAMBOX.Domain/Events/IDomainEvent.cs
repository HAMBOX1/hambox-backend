using MediatR;

namespace HAMBOX.Domain.Events;

/// <summary>
/// Represents an event raised by the domain model.
/// </summary>
public interface IDomainEvent : INotification
{
}
