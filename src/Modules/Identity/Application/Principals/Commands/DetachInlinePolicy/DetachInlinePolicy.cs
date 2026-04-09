using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.DetachInlinePolicy;

public record DetachInlinePolicyCommand(Guid PrincipalId, Guid PolicyId) : ICommand<Success>;

public class DetachInlinePolicyCommandValidator : AbstractValidator<DetachInlinePolicyCommand>
{
    public DetachInlinePolicyCommandValidator()
    {
        RuleFor(x => x.PrincipalId).NotEmpty();
        RuleFor(x => x.PolicyId).NotEmpty();
    }
}

public sealed class DetachInlinePolicyCommandHandler : IRequestHandler<DetachInlinePolicyCommand, ErrorOr<Success>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly ILogger<DetachInlinePolicyCommandHandler> _logger;

    public DetachInlinePolicyCommandHandler(IPrincipalRepository principalRepository, ILogger<DetachInlinePolicyCommandHandler> logger)
    {
        _principalRepository = principalRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(DetachInlinePolicyCommand request, CancellationToken cancellationToken)
    {
        var principal = await _principalRepository.GetById(request.PrincipalId);
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");

        principal.RemoveInlinePolicy(request.PolicyId);
        _logger.LogInformation("Inline policy {PolicyId} detached from Principal {PrincipalId}.", request.PolicyId, request.PrincipalId);

        return Result.Success;
    }
}
