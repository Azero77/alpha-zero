using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.AttachManagedPolicy;

public record AttachManagedPolicyCommand(Guid PrincipalId, Guid ManagedPolicyId) : ICommand<Success>;

public class AttachManagedPolicyCommandValidator : AbstractValidator<AttachManagedPolicyCommand>
{
    public AttachManagedPolicyCommandValidator()
    {
        RuleFor(x => x.PrincipalId).NotEmpty();
        RuleFor(x => x.ManagedPolicyId).NotEmpty();
    }
}

public sealed class AttachManagedPolicyCommandHandler : IRequestHandler<AttachManagedPolicyCommand, ErrorOr<Success>>
{
    private readonly IManagedPolicyRepository _managedPolicyRepository;
    private readonly IPrincipalRepository _principalRepository;
    private readonly ILogger<AttachManagedPolicyCommandHandler> _logger;

    public AttachManagedPolicyCommandHandler(
        IManagedPolicyRepository managedPolicyRepository,
        IPrincipalRepository principalRepository,
        ILogger<AttachManagedPolicyCommandHandler> logger)
    {
        _managedPolicyRepository = managedPolicyRepository;
        _principalRepository = principalRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(AttachManagedPolicyCommand request, CancellationToken cancellationToken)
    {
        var principal = await _principalRepository.GetById(request.PrincipalId);
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");

        var managedPolicy = await _managedPolicyRepository.GetById(request.ManagedPolicyId);
        if (managedPolicy is null) return Error.NotFound("ManagedPolicy.NotFound", "Managed policy not found.");

        principal.AddPolicy(managedPolicy);
        _principalRepository.Update(principal);
        
        _logger.LogInformation("Managed policy {ManagedPolicyId} attached to Principal {PrincipalId}.", request.ManagedPolicyId, request.PrincipalId);

        return Result.Success;
    }
}
