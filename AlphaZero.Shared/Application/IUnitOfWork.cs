namespace AlphaZero.Shared.Application;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();
}
