using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.DetachManagedPolicy;

public record DetachManagedPolicyCommand(Guid PrincipalId, Guid ManagedPolicyId) : ICommand<Success>;

public class DetachManagedPolicyCommandValidator : AbstractValidator<DetachManagedPolicyCommand>
{
    public DetachManagedPolicyCommandValidator()
    {
        RuleFor(x => x.PrincipalId).NotEmpty();
        RuleFor(x => x.ManagedPolicyId).NotEmpty();
    }
}

public sealed class DetachManagedPolicyCommandHandler : IRequestHandler<DetachManagedPolicyCommand, ErrorOr<Success>>
{
    private readonly IManagedPolicyRepository _managedPolicyRepository;
    private readonly ILogger<DetachManagedPolicyCommandHandler> _logger;

    public DetachManagedPolicyCommandHandler(IManagedPolicyRepository managedPolicyRepository, ILogger<DetachManagedPolicyCommandHandler> logger)
    {
        _managedPolicyRepository = managedPolicyRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(DetachManagedPolicyCommand request, CancellationToken cancellationToken)
    {
        await _managedPolicyRepository.RemovePolicyFromPrincipal(request.PrincipalId, request.ManagedPolicyId);
        _logger.LogInformation("Managed policy {ManagedPolicyId} detached from Principal {PrincipalId}.", request.ManagedPolicyId, request.PrincipalId);

        return Result.Success;
    }
}
