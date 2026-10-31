using AlphaZero.Modules.Courses.Application.Enrollments;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Reactivate;
using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Suspend;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Enrollments.Commands;

/// <summary>
/// Story #10 actor guards (rework plan §3.5/§4 step 4): one pre-request guard per lifecycle
/// command. They run at the very front of the MediatR pipeline — BEFORE the aggregate is loaded —
/// and admit exactly the two allowed actor kinds:
/// 1. Payments revocation saga: transport-claimed automation (see EnrollmentTransitionAutomation);
/// 2. tenant-admin/syt: a call that passed an access-controlled endpoint (AccessControl envelope).
/// Registration is by assembly scan (AddPreRequestHandlersFromAssembly) + the open
/// PreRequestBehavior already on the module pipeline.
/// </summary>
public abstract class EnrollmentTransitionGuardBase<TRequest> : IPreRequestHandler<TRequest>
    where TRequest : ICommand<Success>
{
    public Task<ErrorOr<Success>> PreHandle(TRequest request, AuthorizationContext? context, CancellationToken cancellationToken)
        => Task.FromResult(EnrollmentTransitionActor.EnsureCanFire(context));
}

/// <summary>Billing violation / enforcement — story #10 says "no random in-process caller".</summary>
public sealed class SuspendEnrollmentActorGuard : EnrollmentTransitionGuardBase<SuspendEnrollmentCommand>;

/// <summary>Saga-keyed deactivation (revocation surface) — allowed ONLY for the claimed saga.</summary>
public sealed class DeactivateEnrollmentActorGuard : EnrollmentTransitionGuardBase<DeactivateEnrollmentCommand>;

/// <summary>Admin/operator deactivation (#29) — same tenant-admin gate as the saga-keyed command.</summary>
public sealed class DeactivateEnrollmentByIdActorGuard : EnrollmentTransitionGuardBase<DeactivateEnrollmentByIdCommand>;

/// <summary>Resumption flows (stories #1/#2/#4) are equally sensitive — same gate.</summary>
public sealed class ReactivateEnrollmentActorGuard : EnrollmentTransitionGuardBase<ReactivateEnrollmentCommand>;
