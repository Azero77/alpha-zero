using ErrorOr;

namespace AlphaZero.Shared.Domain;

public interface ISoftDeletable : IEntity
{
    public bool IsDeleted { get; }
    public DateTime? OnDeleted { get; }
}
