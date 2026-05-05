using MediatR;

namespace EWallet.Domain.Interfaces;

/// <summary>Marker interface for all domain events raised by entities.</summary>
public interface IDomainEvent : INotification
{
    /// <summary>Unique identifier for this event instance.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event was raised.</summary>
    DateTime OccurredOn { get; }
}
