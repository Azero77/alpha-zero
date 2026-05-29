using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Library.Application.Resolvers;

public class LibraryTenantResolver(ILibraryRepository libraryRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Library;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var library = await libraryRepository.GetById(resourceId);
        return library?.TenantId;
    }
}
