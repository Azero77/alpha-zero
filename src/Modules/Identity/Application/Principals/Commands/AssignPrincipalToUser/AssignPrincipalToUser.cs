using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.Extensions.Caching.Hybrid;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.AssignPrincipalToUser;

public record AssignPrincipalToUserCommand(
    Guid TenantUserId,
    Guid PrincipalId,
    string ResourceArn) : ICommand<Guid>;

public class AssignPrincipalToUserCommandValidator : AbstractValidator<AssignPrincipalToUserCommand>
{
    public AssignPrincipalToUserCommandValidator()
    {
        RuleFor(x => x.TenantUserId).NotEmpty();
        RuleFor(x => x.PrincipalId).NotEmpty();
        RuleFor(x => x.ResourceArn).NotEmpty();
    }
}

public sealed class AssignPrincipalToUserCommandHandler : IRequestHandler<AssignPrincipalToUserCommand, ErrorOr<Guid>>
{
    private readonly ITenantUserPrincipalAssignmentRepository _assignmentRepository;
    private readonly IRepository<TenantUser> _userRepository;
    private readonly IPrincipalRepository _principalRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AssignPrincipalToUserCommandHandler> _logger;

    public AssignPrincipalToUserCommandHandler(
        ITenantUserPrincipalAssignmentRepository assignmentRepository,
        IRepository<TenantUser> userRepository,
        IPrincipalRepository principalRepository,
        ITenantProvider tenantProvider,
        ILogger<AssignPrincipalToUserCommandHandler> logger)
    {
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _principalRepository = principalRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(AssignPrincipalToUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        // Check for existing assignment to prevent unique index violation
        if (await _assignmentRepository.Any(a => a.TenantUser.Id == request.TenantUserId && 
                                                 a.Principal.Id == request.PrincipalId && 
                                                 a.Resource.Value == request.ResourceArn.ToLowerInvariant(), 
                                           cancellationToken))
        {
            return Error.Conflict("Assignment.Duplicate", "This principal is already assigned to this user for this resource.");
        }

        var user = await _userRepository.GetById(request.TenantUserId);
        if (user is null) return Error.NotFound("User.NotFound", "User not found.");

        var principal = await _principalRepository.GetById(request.PrincipalId);
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");

        var result = TenantUserPrincipalAssignment.Create(tenantId.Value, user, principal, request.ResourceArn);
        if (result.IsError) return result.Errors;

        _assignmentRepository.Add(result.Value);

        _logger.LogInformation("Principal {PrincipalId} assigned to User {UserId} for Resource {ResourceArn} in Tenant {TenantId}.", 
            request.PrincipalId, request.TenantUserId, request.ResourceArn, tenantId.Value);

        return result.Value.Id;
    }
}
