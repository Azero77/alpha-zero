using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;

/// <summary>
/// Pre-request visibility for the student course-detail view (#32). Mirrors the
/// GetCourseOverview pre-request family: Published courses open to anyone; draft,
/// under-review, approved and archived courses are masked from anonymous and
/// unauthorized callers with 404 (existence masking). The re-authorization permission
/// is <c>courses:View</c> — the same contract the admin course-detail endpoint uses.
/// </summary>
public class GetCourseViewPreRequestHandler : IPreRequestHandler<GetCourseViewQuery>
{
    private readonly ICourseQueryService _courseQueryService;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;

    public GetCourseViewPreRequestHandler(
        ICourseQueryService courseQueryService,
        IPolicyEvaluatorService policyEvaluatorService)
    {
        _courseQueryService = courseQueryService;
        _policyEvaluatorService = policyEvaluatorService;
    }

    public async Task<ErrorOr<Success>> PreHandle(
        GetCourseViewQuery request,
        AuthorizationContext? context,
        CancellationToken cancellationToken)
    {
        var status = await _courseQueryService.GetCourseStatusAsync(request.CourseId, cancellationToken);
        if (status is null)
        {
            return NotFoundMasked;
        }

        if (status == "Published")
        {
            return Result.Success;
        }

        // Unpublished courses (Draft, UnderReview, Approved, Archived) are masked from
        // anonymous and unauthorized callers
        if (context is null)
        {
            return NotFoundMasked;
        }

        var tenantId = context.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            return NotFoundMasked;
        }

        var arn = ResourceArn.ForCourse(tenantId.Value, request.CourseId);
        var targetContext = context.ForResource("courses:View", arn);

        var authResult = await _policyEvaluatorService.Authorize(targetContext);
        if (authResult.IsError)
        {
            return NotFoundMasked;
        }

        return Result.Success;
    }

    private static Error NotFoundMasked => Error.NotFound("Course.NotFound", "Course not found.");
}
