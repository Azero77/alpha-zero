using MediatR;

namespace AlphaZero.Shared.Authorization;

public record GetResourceTenantIdQuery(ResourceType Type, Guid ResourceId) : IRequest<Guid?>;
