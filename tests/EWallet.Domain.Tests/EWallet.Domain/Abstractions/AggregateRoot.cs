namespace EWallet.Domain.Abstractions;

/// <summary>Base marker for all domain events.</summary>
public abstract record DomainEvent;

/// <summary>Base class for aggregate roots that emit domain events.</summary>
public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(DomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents()
        => _domainEvents.Clear();
}
