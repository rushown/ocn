using EWallet.Domain.Interfaces;

namespace EWallet.Domain.Events;

/// <summary>Abstract base record for all domain events. Provides default <see cref="EventId"/> and <see cref="OccurredOn"/>.</summary>
public abstract record BaseEvent : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
