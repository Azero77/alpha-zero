using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Domain.Services;

public class CoursePublishingService
{
    private readonly CourseReadinessService _readinessService;

    public CoursePublishingService(CourseReadinessService readinessService)
    {
        _readinessService = readinessService;
    }

    public ErrorOr<Success> Publish(Course course, CourseOverview? overview, bool canDirectPublish = false, IEnumerable<CourseAsset>? assets = null)
    {
        if (course.Status != CourseStatus.Approved && !(canDirectPublish && course.Status == CourseStatus.Draft))
        {
            return Error.Conflict("Course.Status",
                canDirectPublish
                    ? "Only draft or approved courses can be published."
                    : "Only approved courses can be published.");
        }

        if (course.Plans.Count == 0)
            return Error.Validation("Course.NoPlans", "Course must have at least one plan before it can be published.");

        var readiness = _readinessService.Evaluate(course, overview, assets);
        if (canDirectPublish && course.Status == CourseStatus.Draft && !readiness.CanPublish)
        {
            return Error.Failure("Course.NotReady",
                readiness.BlockingReasons.Count > 0
                    ? string.Join("; ", readiness.BlockingReasons)
                    : "Course is not ready for publishing.");
        }

        if (!readiness.Checks.Curriculum.AllAttachedAssetsReady)
        {
            return Error.Failure("Course.NotReady",
                readiness.BlockingReasons.Count > 0
                    ? string.Join("; ", readiness.BlockingReasons)
                    : "Course has attached assets that are not ready.");
        }

        return course.Publish();
    }
}
