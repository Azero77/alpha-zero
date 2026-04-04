using ErrorOr;

namespace AlphaZero.Shared.Domain;

public interface ISoftDeleteItem : IEntity
{
    public bool IsDeleted { get; }
}
