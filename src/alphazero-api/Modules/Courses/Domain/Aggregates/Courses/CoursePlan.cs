using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CoursePlan : Entity
{
    public Guid CourseId { get; private set; }
    public string Name { get; private set; }
    public Guid PrincipalId { get; private set; }

    private CoursePlan(Guid id, Guid courseId, string name, Guid principalId) : base(id)
    {
        CourseId = courseId;
        Name = name;
        PrincipalId = principalId;
    }

    public static CoursePlan Create(Guid courseId, string name, Guid principalId)
    {
        return new CoursePlan(Guid.NewGuid(), courseId, name, principalId);
    }

    public void Update(string name, Guid principalId)
    {
        Name = name;
        PrincipalId = principalId;
    }
}
