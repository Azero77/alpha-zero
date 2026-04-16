using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.AssignPrincipalToUser;

public record AssignPrincipalToUserCommand(
    Guid TenantUserId,
    Guid PrincipalTemplateId,
    string ResourceArn) : ICommand<Guid>;

public class AssignPrincipalToUserCommandValidator : AbstractValidator<AssignPrincipalToUserCommand>
{
    public AssignPrincipalToUserCommandValidator()
    {
        RuleFor(x => x.TenantUserId).NotEmpty();
        RuleFor(x => x.PrincipalTemplateId).NotEmpty();
        RuleFor(x => x.ResourceArn).NotEmpty();
    }
}

public sealed class AssignPrincipalToUserCommandHandler : IRequestHandler<AssignPrincipalToUserCommand, ErrorOr<Guid>>
{
    private readonly ITenantUserPrincpialAssignmentRepository _assignmentRepository;
    private readonly IRepository<TenantUser> _userRepository;
    private readonly IRepository<PrincipalTemplate> _templateRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AssignPrincipalToUserCommandHandler> _logger;

    public AssignPrincipalToUserCommandHandler(
        ITenantUserPrincpialAssignmentRepository assignmentRepository,
        IRepository<TenantUser> userRepository,
        IRepository<PrincipalTemplate> templateRepository,
        ITenantProvider tenantProvider,
        ILogger<AssignPrincipalToUserCommandHandler> logger)
    {
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _templateRepository = templateRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(AssignPrincipalToUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        // Check for existing assignment to prevent unique index violation
        // index: "TenantUserId", "PrincipalTemplateId", "Resource"
        if (await _assignmentRepository.Any(a => a.TenantUser.Id == request.TenantUserId && 
                                                 a.Principal.Id == request.PrincipalTemplateId && 
                                                 a.Resource.Value == request.ResourceArn.ToLowerInvariant(), 
                                           cancellationToken))
        {
            return Error.Conflict("Assignment.Duplicate", "This principal is already assigned to this user for this resource.");
        }

        var user = await _userRepository.GetById(request.TenantUserId);
        if (user is null) return Error.NotFound("User.NotFound", "User not found.");

        var template = await _templateRepository.GetById(request.PrincipalTemplateId);
        if (template is null) return Error.NotFound("PrincipalTemplate.NotFound", "Principal template not found.");

        var result = TenantUserPrinciaplAssignment.Create(tenantId.Value, user, template, request.ResourceArn);
        if (result.IsError) return result.Errors;

        _assignmentRepository.Add(result.Value);
        _logger.LogInformation("Principal {TemplateId} assigned to User {UserId} for Resource {ResourceArn} in Tenant {TenantId}.", 
            request.PrincipalTemplateId, request.TenantUserId, request.ResourceArn, tenantId.Value);

        return result.Value.Id;
    }
}
