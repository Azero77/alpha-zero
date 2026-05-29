using MediatR;

namespace AlphaZero.Shared.Authorization;

public class GetResourceTenantIdHandler(IEnumerable<IResourceTenantResolver> resolvers) : IRequestHandler<GetResourceTenantIdQuery, Guid?>
{
    public async Task<Guid?> Handle(GetResourceTenantIdQuery request, CancellationToken cancellationToken)
    {
        var resolver = resolvers.FirstOrDefault(r => r.ResourceType == request.Type);
        if (resolver == null) return null;

        return await resolver.ResolveTenantIdAsync(request.ResourceId, cancellationToken);
    }
}
