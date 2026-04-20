using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Policies.Commands.CreateManagedPolicy;

public record CreateManagedPolicyCommand(
    string Name,
    List<PolicyTemplateStatement> Statements) : ICommand<Guid>;

public class CreateManagedPolicyCommandValidator : AbstractValidator<CreateManagedPolicyCommand>
{
    public CreateManagedPolicyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Statements).NotEmpty();
    }
}

public sealed class CreateManagedPolicyCommandHandler : IRequestHandler<CreateManagedPolicyCommand, ErrorOr<Guid>>
{
    private readonly IManagedPolicyRepository _managedPolicyRepository;
    private readonly ILogger<CreateManagedPolicyCommandHandler> _logger;

    public CreateManagedPolicyCommandHandler(IManagedPolicyRepository managedPolicyRepository, ILogger<CreateManagedPolicyCommandHandler> logger)
    {
        _managedPolicyRepository = managedPolicyRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(CreateManagedPolicyCommand request, CancellationToken cancellationToken)
    {
        if (await _managedPolicyRepository.Any(p => p.Name == request.Name, cancellationToken))
        {
            return Error.Conflict("ManagedPolicy.DuplicateName", $"A managed policy with the name '{request.Name}' already exists.");
        }

        var policyId = Guid.NewGuid();

        var managedPolicy = new ManagedPolicy(policyId, request.Name, request.Statements);

        _managedPolicyRepository.Add(managedPolicy);
        _logger.LogInformation("Managed Policy Template '{PolicyName}' created with ID {PolicyId}.", request.Name, policyId);

        return policyId;
    }
}
