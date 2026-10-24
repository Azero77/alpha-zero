using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Shared.Infrastructure.Caching;

/// <summary>
/// Runs inside <c>UnitOfWork.SaveChangesAsync</c> before the database write: mutation is a
/// persistence concern, not a command concern, so eviction cannot be forgotten by a new command.
/// </summary>
public interface IEvictionStrategy
{
    Task CollectAsync(DbContext context, CancellationToken cancellationToken);
}
