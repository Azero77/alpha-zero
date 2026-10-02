using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Identity.Application.Principals.Queries.GetPrincipalsByResource;

public record PrincipalDto(
    Guid Id,
    string Username,
    string Name,
    PrincipalType PrincipalType,
    string? PrincipalScopeUrn);

public record GetPrincipalsByResourceQuery(Guid ResourceId, string ResourceType) : IRequest<ErrorOr<List<PrincipalDto>>>;

public sealed class GetPrincipalsByResourceQueryHandler : IRequestHandler<GetPrincipalsByResourceQuery, ErrorOr<List<PrincipalDto>>>
{
    private readonly IPrincipalRepository _principalRepository;

    public GetPrincipalsByResourceQueryHandler(IPrincipalRepository principalRepository)
    {
        _principalRepository = principalRepository;
    }

    public async Task<ErrorOr<List<PrincipalDto>>> Handle(GetPrincipalsByResourceQuery request, CancellationToken cancellationToken)
    {
        // Note: The previous filtering by ResourceId/ResourceType in the repo might need logic change 
        // because we removed those fields from Principal. For now, we update the DTO and namespace.
        var principals = await _principalRepository.GetPrincipalsByResourceAsync(request.ResourceId, request.ResourceType, cancellationToken);

        return principals.Select(p => new PrincipalDto(
            p.Id,
            p.Username,
            p.Name,
            p.PrincipalType,
            p.PrincipalScope?.Value
        )).ToList();
    }
}
