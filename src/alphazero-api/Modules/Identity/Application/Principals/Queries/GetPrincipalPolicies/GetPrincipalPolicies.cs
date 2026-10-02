using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Identity.Application.Principals.Queries.GetPrincipalPolicies;

public record PrincipalPoliciesDto(
    Guid PrincipalId,
    List<PolicyDto> InlinePolicies,
    List<ManagedPolicyDto> ManagedPolicies);

public record PolicyDto(Guid Id, string Name, List<PolicyStatement> Statements);
public record ManagedPolicyDto(Guid Id, string Name, List<ManagedPolicyStatement> Statements);

public record GetPrincipalPoliciesQuery(Guid PrincipalId) : IRequest<ErrorOr<PrincipalPoliciesDto>>;

public sealed class GetPrincipalPoliciesQueryHandler : IRequestHandler<GetPrincipalPoliciesQuery, ErrorOr<PrincipalPoliciesDto>>
{
    private readonly IPrincipalRepository _principalRepository;

    public GetPrincipalPoliciesQueryHandler(IPrincipalRepository principalRepository)
    {
        _principalRepository = principalRepository;
    }

    public async Task<ErrorOr<PrincipalPoliciesDto>> Handle(GetPrincipalPoliciesQuery request, CancellationToken cancellationToken)
    {
        var principal = await _principalRepository.GetById(request.PrincipalId);
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");

        var inlinePolicies = principal.Policies
            .OfType<InlinePolicy>()
            .Select(p => new PolicyDto(p.Id, p.Name, p.Statements.ToList()))
            .ToList();

        var managedPolicies = principal.Policies
            .OfType<ManagedPolicy>()
            .Select(m => new ManagedPolicyDto(m.Id, m.Name, m.Statements.ToList()))
            .ToList();

        return new PrincipalPoliciesDto(principal.Id, inlinePolicies, managedPolicies);
    }
}
