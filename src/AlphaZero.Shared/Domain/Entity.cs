namespace AlphaZero.Shared.Domain;

public class Entity : IEntity
{
    public Guid Id { get; private set; }
    protected Entity(Guid id)
    {
        Id = id;
    }
    protected Entity()
    {
    }
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;
        return ((Entity)obj).Id == Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

public interface IEntity
{
    public Guid Id { get; }
}