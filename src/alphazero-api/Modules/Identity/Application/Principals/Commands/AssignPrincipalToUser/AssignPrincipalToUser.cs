using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using AlphaZero.Shared.Domain;

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
    private readonly IClock _clock;

    public AssignPrincipalToUserCommandHandler(
        ITenantUserPrincipalAssignmentRepository assignmentRepository,
        IRepository<TenantUser> userRepository,
        IPrincipalRepository principalRepository,
        ITenantProvider tenantProvider,
        ILogger<AssignPrincipalToUserCommandHandler> logger,
        IClock clock
        )
    {
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _principalRepository = principalRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _clock = clock;
    }

    public async Task<ErrorOr<Guid>> Handle(AssignPrincipalToUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
        {
            if (ResourceArn.TryParse(request.ResourceArn, out var parsedArn) &&
                Guid.TryParse(parsedArn.TenantIdString, out var fallbackTenantId))
            {
                tenantId = fallbackTenantId;
            }
            else
            {
                return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");
            }
        }

        var normalizedArn = request.ResourceArn.ToLowerInvariant();

        // Check for existing assignment to ensure idempotency and prevent unique index violation.
        // MAPPED-COLUMNS + in-memory ARN match: traversing .TenantUser makes the tenant-filtered
        // nav JOIN untranslatable, `.Resource.Value` member access on the converted ARN property is
        // untranslatable, and value-object equality (ErrorOr) is untranslatable — so narrow in SQL
        // on TenantUserId/PrincipalId (bounded rows: one per resource scope) and match the ARN
        // client-side, mirroring the existing GetActiveAssignment discipline.
        var candidates = await _assignmentRepository.Get(
            a => a.TenantUserId == request.TenantUserId && a.PrincipalId == request.PrincipalId,
            cancellationToken);

        var existingAssignment = candidates.FirstOrDefault(c => c.Resource.Value == normalizedArn);

        if (existingAssignment is not null)
        {
            _logger.LogInformation(
                "Principal {PrincipalId} was already assigned to User {UserId} for Resource {ResourceArn}. Returning existing assignment ID {AssignmentId}.",
                request.PrincipalId, request.TenantUserId, request.ResourceArn, existingAssignment.Id);

            return existingAssignment.Id;
        }

        var user = await _userRepository.GetById(request.TenantUserId);
        if (user is null) return Error.NotFound("User.NotFound", "User not found.");

        var principal = await _principalRepository.GetById(request.PrincipalId);
        if (principal is null) return Error.NotFound("Principal.NotFound", "Principal not found.");

        var result = TenantUserPrincipalAssignment.Create(tenantId.Value, user, principal, request.ResourceArn, _clock.Now);
        if (result.IsError) return result.Errors;

        _assignmentRepository.Add(result.Value);

        _logger.LogInformation("Principal {PrincipalId} assigned to User {UserId} for Resource {ResourceArn} in Tenant {TenantId}.", 
            request.PrincipalId, request.TenantUserId, request.ResourceArn, tenantId.Value);

        return result.Value.Id;
    }
}
