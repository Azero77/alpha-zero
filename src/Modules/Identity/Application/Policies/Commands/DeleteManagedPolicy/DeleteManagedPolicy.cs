using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Policies.Commands.DeleteManagedPolicy;

public record DeleteManagedPolicyCommand(Guid PolicyId) : ICommand<Success>;

public class DeleteManagedPolicyCommandValidator : AbstractValidator<DeleteManagedPolicyCommand>
{
    public DeleteManagedPolicyCommandValidator()
    {
        RuleFor(x => x.PolicyId).NotEmpty();
    }
}

public sealed class DeleteManagedPolicyCommandHandler : IRequestHandler<DeleteManagedPolicyCommand, ErrorOr<Success>>
{
    private readonly IManagedPolicyRepository _managedPolicyRepository;
    private readonly ILogger<DeleteManagedPolicyCommandHandler> _logger;

    public DeleteManagedPolicyCommandHandler(IManagedPolicyRepository managedPolicyRepository, ILogger<DeleteManagedPolicyCommandHandler> logger)
    {
        _managedPolicyRepository = managedPolicyRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteManagedPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _managedPolicyRepository.GetById(request.PolicyId);
        if (policy is null) return Error.NotFound("ManagedPolicy.NotFound", "Managed policy template not found.");

        _managedPolicyRepository.Remove(policy);
        _logger.LogInformation("Managed Policy Template {PolicyId} deleted.", request.PolicyId);

        return Result.Success;
    }
}
