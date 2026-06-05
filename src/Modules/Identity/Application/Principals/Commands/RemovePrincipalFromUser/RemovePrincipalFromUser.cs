using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.RemovePrincipalFromUser;

public record RemovePrincipalFromUserCommand(
    Guid TenantUserId,
    Guid PrincipalId,
    string ResourceArn) : ICommand<Success>;

public sealed class RemovePrincipalFromUserCommandHandler(
    ITenantUserPrincpialAssignmentRepository repository,
    ILogger<RemovePrincipalFromUserCommandHandler> logger) : IRequestHandler<RemovePrincipalFromUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(RemovePrincipalFromUserCommand request, CancellationToken cancellationToken)
    {
        var assignment = await repository.Get(request.TenantUserId, request.ResourceArn);
        
        if (assignment == null || assignment.Principal.Id != request.PrincipalId)
        {
            return Error.NotFound("Assignment.NotFound", "Principal assignment not found for this user, resource, and role.");
        }

        repository.Remove(assignment);
        logger.LogWarning("Principal {PrincipalId} removed from User {UserId} for Resource {ResourceArn}.", 
            request.PrincipalId, request.TenantUserId, request.ResourceArn);

        return Result.Success;
    }
}
