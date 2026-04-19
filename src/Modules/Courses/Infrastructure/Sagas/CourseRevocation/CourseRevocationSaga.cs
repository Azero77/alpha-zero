using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRevocation;

public class CourseRevocationSaga : MassTransitStateMachine<CourseRevocationState>
{
    private readonly ILogger<CourseRevocationSaga> _logger;

    public CourseRevocationSaga(ILogger<CourseRevocationSaga> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        Event(() => AccessCodeRevoked, x => x.CorrelateById(context => context.Message.EventId));
        Event(() => EnrollmentRevoked, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => RoleRemoved, x => x.CorrelateById(context => context.Message.CorrelationId));

        Initially(
            When(AccessCodeRevoked)
                .Then(context =>
                {
                    context.Saga.UserId = context.Message.UserId;
                    context.Saga.AccessCodeId = context.Message.AccessCodeId;
                    context.Saga.ResourceArn = context.Message.Resource.ToString();
                    context.Saga.CreatedAt = DateTime.UtcNow;
                    
                    _logger.LogWarning("Course Revocation Saga Started for Access Code {AccessCode} and User {UserId}", 
                        context.Message.AccessCodeId, context.Message.UserId);
                })
                .TransitionTo(RevokingEnrollment)
                .Send(context => new RevokeStudentEnrollmentFromSagaCommand(
                    context.Saga.CorrelationId,
                    context.Saga.UserId,
                    context.Message.Resource.GetCourseId()))
        );

        During(RevokingEnrollment,
            When(EnrollmentRevoked)
                .Then(context =>
                {
                    context.Saga.RevokedAt = DateTime.UtcNow;
                    _logger.LogInformation("Enrollment for User {UserId} Revoked. Proceeding to remove Authorization.", context.Saga.UserId);
                })
                .TransitionTo(RemovingAuthorization)
                .Send(context => new RemoveStudentRoleFromSagaCommand(
                    context.Saga.CorrelationId,
                    context.Saga.UserId,
                    AlphaZero.Shared.Domain.ResourceArn.Create(context.Saga.ResourceArn).Value))
        );

        During(RemovingAuthorization,
            When(RoleRemoved)
                .Then(context =>
                {
                    context.Saga.UnauthorizedAt = DateTime.UtcNow;
                    _logger.LogInformation("Authorization removal completed for User {UserId}. Revocation Saga Finalized.", context.Saga.UserId);
                })
                .Finalize(),

            When(StudentRoleRemovalFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    _logger.LogCritical("CRITICAL: Failed to remove Identity permissions for User {UserId} during revocation. Manual intervention required.", context.Saga.UserId);
                })
                .TransitionTo(Failed)
        );

        During(RevokingEnrollment,
            When(StudentEnrollmentRevocationFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    _logger.LogCritical("CRITICAL: Failed to deactivate enrollment for User {UserId} during revocation. Manual intervention required.", context.Saga.UserId);
                })
                .TransitionTo(Failed)
        );
    }

    public State RevokingEnrollment { get; private set; } = null!;
    public State RemovingAuthorization { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<AccessCodeRevokedIntegrationEvent> AccessCodeRevoked { get; private set; } = null!;
    public Event<StudentEnrollmentRevokedFromSagaEvent> EnrollmentRevoked { get; private set; } = null!;
    public Event<StudentRoleRemovedFromSagaEvent> RoleRemoved { get; private set; } = null!;
    public Event<StudentEnrollmentRevocationFailedEvent> StudentEnrollmentRevocationFailed { get; private set; } = null!;
    public Event<StudentRoleRemovalFailedEvent> StudentRoleRemovalFailed { get; private set; } = null!;
}
