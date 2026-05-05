using EWallet.Domain.Interfaces;

namespace EWallet.Domain.Entities;

/// <summary>
/// Base class for all domain entities. Provides identity, audit timestamps,
/// and a domain-event collection.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Unique identifier for this entity.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>UTC timestamp when the entity was first persisted.</summary>
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last mutation, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>Domain events raised during the current unit of work.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Queues a domain event to be dispatched after the unit of work commits.</summary>
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Clears all queued domain events. Called by the infrastructure layer after dispatch.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Updates the <see cref="UpdatedAt"/> timestamp to now (UTC).</summary>
    protected void SetUpdated() => UpdatedAt = DateTime.UtcNow;
}
