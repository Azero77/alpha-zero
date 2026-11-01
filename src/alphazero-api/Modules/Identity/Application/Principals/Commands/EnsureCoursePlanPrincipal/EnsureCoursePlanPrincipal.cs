using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.Application.Principals.Commands.EnsureCoursePlanPrincipal;

/// <summary>
/// Creates (or returns the existing) ROLE principal that owns one course-plan's access policy —
/// issue #34 rework §3.2.1 (step 9): "take a plan, course and make a principal for it" — a
/// principal per (course, plan), carrying a <b>managed</b> policy; never inline policies (§5 rule 1).
/// The policy allows <c>courses:View</c>/<c>courses:Read</c> scoped by the caller's enrollment
/// assignment resource (the course ARN — <c>TenantUserAuthorizationStrategy</c> injects it at
/// evaluation time), gated by the <see cref="Operator.AccessTicketValid"/> condition.
/// Courses calls this through <see cref="IIdentityModule"/> when a plan carries an access window;
/// the operation is idempotent by the deterministic (tenant, course, plan) identity.
/// </summary>
public record EnsureCoursePlanPrincipalCommand(Guid CourseId, Guid PlanId) : ICommand<Guid>;

public class EnsureCoursePlanPrincipalCommandValidator : AbstractValidator<EnsureCoursePlanPrincipalCommand>
{
    public EnsureCoursePlanPrincipalCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
    }
}

public sealed class EnsureCoursePlanPrincipalCommandHandler : IRequestHandler<EnsureCoursePlanPrincipalCommand, ErrorOr<Guid>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly IManagedPolicyRepository _managedPolicyRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<EnsureCoursePlanPrincipalCommandHandler> _logger;

    public EnsureCoursePlanPrincipalCommandHandler(
        IPrincipalRepository principalRepository,
        IManagedPolicyRepository managedPolicyRepository,
        ITenantProvider tenantProvider,
        IPasswordHasher passwordHasher,
        ILogger<EnsureCoursePlanPrincipalCommandHandler> logger)
    {
        _principalRepository = principalRepository;
        _managedPolicyRepository = managedPolicyRepository;
        _tenantProvider = tenantProvider;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(EnsureCoursePlanPrincipalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var facts = new CoursePlanPrincipalFacts(tenantId.Value, request.CourseId, request.PlanId);

        var policy = await EnsureManagedPolicyAsync(facts, cancellationToken);

        var existing = await _principalRepository.GetById(facts.PrincipalId, cancellationToken);
        if (existing is not null)
        {
            // Idempotent replay (retry/second plan edit): attach the policy only if missing,
            // never duplicate the principal.
            var hasPolicy = existing.Policies.Any(p => p is AlphaZero.Shared.Domain.Entity e && e.Id == policy.Id);
            if (!hasPolicy)
            {
                existing.AddPolicy(policy);
                _principalRepository.Update(existing);
            }

            return existing.Id;
        }

        // null principal scope ⇒ IsManaged: a role principal that cannot log in (seed precedent).
        var principalResult = Principal.Create(
            facts.PrincipalId,
            facts.Username,
            _passwordHasher.HashPassword("system-role"),
            facts.DisplayName,
            PrincipalType.Role,
            principalScope: null,
            tenantId.Value);

        if (principalResult.IsError) return principalResult.Errors;

        var principal = principalResult.Value;
        principal.AddPolicy(policy);
        _principalRepository.Add(principal);

        _logger.LogInformation(
            "Ensured course-plan principal {PrincipalId} (username {Username}) for course {CourseId} plan {PlanId} in tenant {TenantId}.",
            facts.PrincipalId, facts.Username, request.CourseId, request.PlanId, tenantId.Value);

        return facts.PrincipalId;
    }

    private async Task<ManagedPolicy> EnsureManagedPolicyAsync(CoursePlanPrincipalFacts facts, CancellationToken cancellationToken)
    {
        var existingPolicy = await _managedPolicyRepository.GetFirst(p => p.Name == facts.PolicyName, cancellationToken);
        if (existingPolicy is not null) return existingPolicy;

        // The statement carries NO resource list: the enrollment assignment's course ARN scopes it
        // (managed-policy contract), and the condition rides the statement itself (§3.2.1).
        var conditionNode = new ConditionNode(
            "Id",
            Operator.AccessTicketValid,
            JsonDocument.Parse("true").RootElement);

        var statementResult = ManagedPolicyStatement.Create(
            "AllowWindowedCourseAccess",
            new List<string> { "courses:View", "courses:Read" },
            effect: true,
            conditionNode);
        if (statementResult.IsError)
        {
            // Unreachable with the literal statement above; a guard against future edits.
            throw new InvalidOperationException(
                $"Managed policy statement rejected: {string.Join(", ", statementResult.Errors.Select(e => e.Description))}");
        }

        var policy = new ManagedPolicy(facts.PolicyId, facts.PolicyName, new List<ManagedPolicyStatement> { statementResult.Value });
        _managedPolicyRepository.Add(policy);

        return policy;
    }
}
