using AlphaZero.Shared.Domain;
using FluentAssertions;

namespace AlphaZero.Modules.Courses.UnitTests;

public abstract class DomainTest
{
    protected static T AssertDomainEvent<T>(AggregateRoot aggregate) where T : IDomainEvent
    {
        var domainEvent = aggregate.DomainEvents.OfType<T>().FirstOrDefault();
        domainEvent.Should().NotBeNull($"Domain event of type {typeof(T).Name} was expected but not found.");
        return domainEvent!;
    }
}
