namespace AlphaZero.Shared.Domain;

internal class AggregateRoot : Entity
{
    private List<IDomainEvent> _domainEvents = new();

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents ??= [];

        this._domainEvents.Add(domainEvent);
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public List<IDomainEvent> PopDomainEvents()
    {
        var copy = _domainEvents.ToList();

        _domainEvents.Clear();
        return copy;
    }

}
