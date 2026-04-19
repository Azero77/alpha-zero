using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Shared.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Sagas.CourseRedemption;

public class CourseRedemptionSaga : MassTransitStateMachine<CourseRedemptionState>
{
    private readonly ILogger<CourseRedemptionSaga> _logger;

    public CourseRedemptionSaga(ILogger<CourseRedemptionSaga> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        Event(() => CourseAccessUnlocked, x => x.CorrelateById(context => context.Message.Id));
        Event(() => StudentEnrolled, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => StudentRoleAssigned, x => x.CorrelateById(context => context.Message.CorrelationId));

        Initially(
            When(CourseAccessUnlocked)
                .Then(context =>
                {
                    context.Saga.UserId = context.Message.UserId;
                    context.Saga.AccessCodeId = context.Message.AccessCodeId;
                    context.Saga.CourseArn = context.Message.Resource.ToString();
                    context.Saga.Plan = context.Message.Plan;
                    context.Saga.CreatedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation("Course Redemption Saga Started for Access Code {AccessCode} and User {UserId}", 
                        context.Message.AccessCodeId, context.Message.UserId);
                })
                .TransitionTo(Enrolling)
                .Send(context => new EnrollStudentFromSagaCommand(
                    context.Saga.CorrelationId,
                    context.Saga.UserId,
                    context.Message.Resource.GetCourseId(),
                    context.Message.Resource))
        );

        During(Enrolling,
            When(StudentEnrolled)
                .Then(context =>
                {
                    context.Saga.EnrolledAt = DateTime.UtcNow;
                    _logger.LogInformation("User {UserId} Enrolled. Proceeding to Authorization.", context.Saga.UserId);
                })
                .TransitionTo(Authorizing)
                .Send(context => new AssignStudentRoleFromSagaCommand(
                    context.Saga.CorrelationId,
                    context.Saga.UserId,
                    ResourceArn.Create(context.Saga.CourseArn).Value))
        );

        During(Authorizing,
            When(StudentRoleAssigned)
                .Then(context =>
                {
                    context.Saga.AuthorizedAt = DateTime.UtcNow;
                    _logger.LogInformation("Authorization completed for User {UserId}. Saga Finalized.", context.Saga.UserId);
                })
                .Finalize(),

            When(StudentRoleAssignmentFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    _logger.LogError("Authorization FAILED for User {UserId}. Triggering Compensation (Deactivating Enrollment).", context.Saga.UserId);
                })
                .TransitionTo(Failed)
                .Send(context => new RevokeStudentEnrollmentFromSagaCommand(
                    context.Saga.CorrelationId,
                    context.Saga.UserId,
                    ResourceArn.Create(context.Saga.CourseArn).Value.GetCourseId()))
        );

        During(Enrolling,
            When(StudentEnrollmentFailed)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    _logger.LogError("Enrollment FAILED for User {UserId}. Saga Aborted.", context.Saga.UserId);
                })
                .TransitionTo(Failed)
        );
    }

    public State Enrolling { get; private set; } = null!;
    public State Authorizing { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<CourseAccessUnlockedIntegrationEvent> CourseAccessUnlocked { get; private set; } = null!;
    public Event<StudentEnrolledFromSagaEvent> StudentEnrolled { get; private set; } = null!;
    public Event<StudentRoleAssignedFromSagaEvent> StudentRoleAssigned { get; private set; } = null!;
    public Event<StudentEnrollmentFailedEvent> StudentEnrollmentFailed { get; private set; } = null!;
    public Event<StudentRoleAssignmentFailedEvent> StudentRoleAssignmentFailed { get; private set; } = null!;
}
