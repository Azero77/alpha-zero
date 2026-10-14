using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;

public class GetCourseOverviewPreRequestHandler : IPreRequestHandler<GetCourseOverviewQuery>
{
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;
    private readonly ITenantProvider _tenantProvider;

    public GetCourseOverviewPreRequestHandler(
        ICourseOverviewRepository courseOverviewRepository,
        IPolicyEvaluatorService policyEvaluatorService,
        ITenantProvider tenantProvider)
    {
        _courseOverviewRepository = courseOverviewRepository;
        _policyEvaluatorService = policyEvaluatorService;
        _tenantProvider = tenantProvider;
    }

    public async Task<ErrorOr<Success>> PreHandle(
        GetCourseOverviewQuery request,
        AuthorizationContext? context,
        CancellationToken cancellationToken)
    {
        var overview = await _courseOverviewRepository.GetByCourseIdAsync(request.CourseId, cancellationToken);
        if (overview is null)
        {
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");
        }

        if (overview.CourseStatus == "Published")
        {
            return Result.Success;
        }

        // Unpublished courses (Draft, InReview, Archived) are masked from anonymous and unauthorized users
        if (context is null)
        {
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");
        }

        var tenantId = context.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");
        }

        var arn = ResourceArn.ForCourse(tenantId.Value, request.CourseId);
        var targetContext = context.ForResource("courses.overview:Manage", arn);

        var authResult = await _policyEvaluatorService.Authorize(targetContext);
        if (authResult.IsError)
        {
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");
        }

        return Result.Success;
    }
}
