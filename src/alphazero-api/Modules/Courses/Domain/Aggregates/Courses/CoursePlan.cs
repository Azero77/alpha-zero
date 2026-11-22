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
    public long CatalogSourceVersion { get; private set; }
    public bool IsRemoved { get; private set; }

    private CoursePlan(Guid id, Guid courseId, string name, Guid principalId, int? accessWindowDays = null) : base(id)
    {
        CourseId = courseId;
        Name = name;
        CatalogSourceVersion = 0;
        IsRemoved = false;
        PrincipalId = principalId;
        AccessWindowDays = accessWindowDays;
    }

    /// <param name="planId">Optional explicit id — the plan-creation flow pre-computes it so the per-(course, plan) principal (§3.2.1) can be minted before the plan aggregates.</param>
    public static CoursePlan Create(Guid courseId, string name, Guid principalId, int? accessWindowDays = null, Guid? planId = null)
    {
        return new CoursePlan(planId ?? Guid.NewGuid(), courseId, name, principalId, accessWindowDays);
    }

    public void Update(string name, Guid principalId, int? accessWindowDays = null)
    {
        Name = name;
        PrincipalId = principalId;
        AccessWindowDays = accessWindowDays;
    }

    /// <summary>
    /// The monotonically increasing per-plan version is persisted on the source aggregate and
    /// stamped into catalog snapshots; timestamps are not suitable for ordering concurrent edits.
    /// </summary>
    public long AdvanceCatalogSourceVersion()
    {
        CatalogSourceVersion = checked(CatalogSourceVersion + 1);
        return CatalogSourceVersion;
    }

    public void MarkRemoved() => IsRemoved = true;

    public void Restore() => IsRemoved = false;
}
