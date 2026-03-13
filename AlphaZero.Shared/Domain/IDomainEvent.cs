using MediatR;

namespace AlphaZero.Shared.Domain;

public interface IDomainEvent : INotification
{
    Guid Id { get;  }
    DateTime OccuredOn { get; }
}

public class DomainEvent : IDomainEvent
{
    public Guid Id { get; }
    public DateTime OccuredOn { get; }

    public DomainEvent()
    {
        Id = Guid.NewGuid();
        OccuredOn = DateTime.UtcNow;
    }
}