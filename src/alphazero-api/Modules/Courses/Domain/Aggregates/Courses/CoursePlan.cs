using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

public class CoursePlan : Entity
{
    public Guid CourseId { get; private set; }
    public string Name { get; private set; }
    public Guid PrincipalId { get; private set; }

    /// <summary>
    /// Optional access window (issue #34 stories #5/#6): enrollments created against this plan
    /// expire automatically after <see cref="AccessWindowDays"/> days. Null = unlimited access.
    /// </summary>
    public int? AccessWindowDays { get; private set; }

    private CoursePlan(Guid id, Guid courseId, string name, Guid principalId, int? accessWindowDays = null) : base(id)
    {
        CourseId = courseId;
        Name = name;
        PrincipalId = principalId;
        AccessWindowDays = accessWindowDays;
    }

    public static CoursePlan Create(Guid courseId, string name, Guid principalId, int? accessWindowDays = null)
    {
        return new CoursePlan(Guid.NewGuid(), courseId, name, principalId, accessWindowDays);
    }

    public void Update(string name, Guid principalId, int? accessWindowDays = null)
    {
        Name = name;
        PrincipalId = principalId;
        AccessWindowDays = accessWindowDays;
    }
}
