using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Events;

public class EnrollementCreatedDomainEvent : DomainEvent
{
    public Guid EnrollementId { get; }
    public Guid StudentId { get; }
    public Guid CourseId { get; }

    public EnrollementCreatedDomainEvent(Guid enrollementId, Guid studentId, Guid courseId)
    {
        EnrollementId = enrollementId;
        StudentId = studentId;
        CourseId = courseId;
    }
}
