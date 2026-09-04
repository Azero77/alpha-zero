using AlphaZero.Shared.Domain;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace AlphaZero.Shared.Application;

public interface IUnitOfWork
{
    void TrackEntity(params AggregateRoot[] entities);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
