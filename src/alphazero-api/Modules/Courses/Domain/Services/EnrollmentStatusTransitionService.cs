using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Events;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Domain.Services
{
    public class EnrollmentStatusTransitionService(IPublisher publisher, 
        IEnrollmentRepository repository,
        ILogger<EnrollmentStatusTransitionService> logger)
    {
    
    
        public async Task<ErrorOr<Success>> Suspend(Enrollment enrollment,EnrollmentChangingStatusMetadata metadata, string? reason, CancellationToken token = default)
        {
        
            var prevStatus = enrollment.Status;
            if (prevStatus == EnrollmentStatus.Suspended) return Result.Success;
            var result = enrollment.Suspend(reason);
            if (!result.IsError)
            {
                await publisher.Publish(new EnrollmentDeactivatedDomainEvent(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.TenantId, prevStatus, metadata.operatorId),token);
                repository.Update(enrollment);
                logger.LogWarning("Enrollment {EnrollmentId} for Student {StudentId} in Course {CourseId} has been SUSPENDED (reason: {Reason}).",
                    enrollment.Id, enrollment.StudentId, enrollment.CourseId, reason ?? "unspecified");
                
            }

            return result;
        }

        public async Task<ErrorOr<Success>> Deactivate(Enrollment enrollment,EnrollmentChangingStatusMetadata metadata, CancellationToken token = default)
        {
        
            var prevStatus = enrollment.Status;
        
            if (prevStatus == EnrollmentStatus.Inactive) return Result.Success;
            var result  = enrollment.Deactivate();
            if (!result.IsError)
            {
                await publisher.Publish(new EnrollmentDeactivatedDomainEvent(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.TenantId, prevStatus, metadata.operatorId),token);
                repository.Update(enrollment);
                
                logger.LogWarning("Enrollment {EnrollmentId} for User {UserId} in Course {CourseId} has been DEACTIVATED.",
                    enrollment.Id, enrollment.StudentId, enrollment.CourseId);
            }
            return result;
        }

        public async Task<ErrorOr<Success>> Reactivate(Enrollment enrollment,EnrollmentChangingStatusMetadata metadata, CancellationToken token = default)
        {
            var prevStatus = enrollment.Status;
        
            if (prevStatus == EnrollmentStatus.Active) return Result.Success;
            var result  = enrollment.Deactivate();
            if (!result.IsError)
            {
                await publisher.Publish(new EnrollmentResumedDomainEvent(enrollment.Id, enrollment.StudentId, enrollment.CourseId, enrollment.TenantId, prevStatus, metadata.operatorId),token);
                repository.Update(enrollment);
                
                logger.LogInformation("Enrollment {EnrollmentId} for Student {StudentId} in Course {CourseId} has been REACTIVATED.",
                    enrollment.Id, enrollment.StudentId, enrollment.CourseId);
            }
            return result;
        
        }

        public record EnrollmentChangingStatusMetadata(Guid? operatorId);
    }
}