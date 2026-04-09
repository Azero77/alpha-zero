using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Identity.Application.Principals.Queries.GetPrincipalsByResource;

public record PrincipalDto(
    Guid Id,
    string IdentityId,
    string Name,
    PrincipalType PrincipalType,
    string? PrincipalScopeUrn,
    Guid? ResourceId,
    ResourceType? ScopeResourceType);

public record GetPrincipalsByResourceQuery(Guid ResourceId, ResourceType ResourceType) : IRequest<ErrorOr<List<PrincipalDto>>>;

public sealed class GetPrincipalsByResourceQueryHandler : IRequestHandler<GetPrincipalsByResourceQuery, ErrorOr<List<PrincipalDto>>>
{
    private readonly IPrincipalRepository _principalRepository;

    public GetPrincipalsByResourceQueryHandler(IPrincipalRepository principalRepository)
    {
        _principalRepository = principalRepository;
    }

    public async Task<ErrorOr<List<PrincipalDto>>> Handle(GetPrincipalsByResourceQuery request, CancellationToken cancellationToken)
    {
        var principals = await _principalRepository.GetPrincipalsByResourceAsync(request.ResourceId, request.ResourceType, cancellationToken);

        return principals.Select(p => new PrincipalDto(
            p.Id,
            p.IdentityId,
            p.Name,
            p.PrincipalType,
            p.PrincipalScopeUrn,
            p.ResourceId,
            p.ScopeResourceType
        )).ToList();
    }
}
