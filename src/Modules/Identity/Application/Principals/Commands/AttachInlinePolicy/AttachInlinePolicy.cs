using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.AttachInlinePolicy;

public record AttachInlinePolicyCommand(
    Guid PrincipalId,
    string PolicyName,
    List<PolicyStatement> Statements) : ICommand<Success>;

public class AttachInlinePolicyCommandValidator : AbstractValidator<AttachInlinePolicyCommand>
{
    public AttachInlinePolicyCommandValidator()
    {
        RuleFor(x => x.PrincipalId).NotEmpty();
        RuleFor(x => x.PolicyName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Statements).NotEmpty();
    }
}

public sealed class AttachInlinePolicyCommandHandler : IRequestHandler<AttachInlinePolicyCommand, ErrorOr<Success>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AttachInlinePolicyCommandHandler> _logger;

    public AttachInlinePolicyCommandHandler(
        IPrincipalRepository principalRepository,
        ITenantProvider tenantProvider,
        ILogger<AttachInlinePolicyCommandHandler> logger)
    {
        _principalRepository = principalRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(AttachInlinePolicyCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var principal = await _principalRepository.GetById(request.PrincipalId);
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");

        var policy = new Policy(Guid.NewGuid(), request.PolicyName, tenantId.Value);
        foreach (var statement in request.Statements)
        {
            policy.AddStatement(statement);
        }

        principal.AddInlinePolicy(policy);
        _logger.LogInformation("Inline policy '{PolicyName}' attached to Principal {PrincipalId}.", request.PolicyName, request.PrincipalId);

        return Result.Success;
    }
}
