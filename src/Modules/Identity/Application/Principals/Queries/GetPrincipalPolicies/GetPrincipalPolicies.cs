using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Identity.Application.Principals.Queries.GetPrincipalPolicies;

public record PrincipalPoliciesDto(
    Guid PrincipalId,
    List<PolicyDto> InlinePolicies,
    List<ManagedPolicyDto> ManagedPolicies);

public record PolicyDto(Guid Id, string Name, List<PolicyStatement> Statements);
public record ManagedPolicyDto(Guid Id, string Name, List<PolicyTemplateStatement> Statements);

public record GetPrincipalPoliciesQuery(Guid PrincipalId) : IRequest<ErrorOr<PrincipalPoliciesDto>>;

public sealed class GetPrincipalPoliciesQueryHandler : IRequestHandler<GetPrincipalPoliciesQuery, ErrorOr<PrincipalPoliciesDto>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly IPolicyRepository _policyRepository;

    public GetPrincipalPoliciesQueryHandler(IPrincipalRepository principalRepository, IPolicyRepository policyRepository)
    {
        _principalRepository = principalRepository;
        _policyRepository = policyRepository;
    }

    public async Task<ErrorOr<PrincipalPoliciesDto>> Handle(GetPrincipalPoliciesQuery request, CancellationToken cancellationToken)
    {
        var principal = await _principalRepository.GetById(request.PrincipalId);
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");

        var managedPolicies = await _policyRepository.GetManagedPoliciesForPrincipal(request.PrincipalId);

        var dto = new PrincipalPoliciesDto(
            principal.Id,
            principal.InlinePolicies.Select(p => new PolicyDto(p.Id, p.Name, p.Statements.ToList())).ToList(),
            managedPolicies?.Select(m => new ManagedPolicyDto(m.Id, m.Name, m.Statements)).ToList() ?? new List<ManagedPolicyDto>());

        return dto;
    }
}
