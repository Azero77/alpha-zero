namespace AlphaZero.Modules.Courses.IntegrationEvents;

public record EnrollmentCreatedIntegrationEvent(Guid EnrollmentId, Guid StudentId, Guid CourseId, Guid TenantId = default);
