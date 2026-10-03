using ErrorOr;
using MediatR;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Modules.Courses.Domain.ValueObjects;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.UpsertOverview;

public record UpsertCourseOverviewCommand(
    Guid CourseId,
    string DescriptionContent,
    string? TargetAudienceContent,
    string? LearningObjectivesContent) : IRequest<ErrorOr<Success>>;

public class UpsertCourseOverviewCommandHandler : IRequestHandler<UpsertCourseOverviewCommand, ErrorOr<Success>>
{
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ITenantProvider _tenantProvider;

    public UpsertCourseOverviewCommandHandler(
        ICourseOverviewRepository courseOverviewRepository,
        ICourseRepository courseRepository,
        ITenantProvider tenantProvider)
    {
        _courseOverviewRepository = courseOverviewRepository;
        _courseRepository = courseRepository;
        _tenantProvider = tenantProvider;
    }

    public async Task<ErrorOr<Success>> Handle(UpsertCourseOverviewCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var courseExists = await _courseRepository.Any(x => x.Id == request.CourseId && x.TenantId == tenantId.Value, cancellationToken);
        if (!courseExists)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var overview = await _courseOverviewRepository.GetByCourseIdAsync(request.CourseId, cancellationToken);

        var descDoc = RichText.Create(request.DescriptionContent).Value;
        var targetDoc = request.TargetAudienceContent != null ? RichText.Create(request.TargetAudienceContent).Value : null;
        var learningDoc = request.LearningObjectivesContent != null ? RichText.Create(request.LearningObjectivesContent).Value : null;

        if (overview is null)
        {
            overview = CourseOverview.Create(
                Guid.NewGuid(),
                tenantId.Value,
                request.CourseId,
                descDoc,
                targetDoc,
                learningDoc,
                null);

            _courseOverviewRepository.Add(overview);
        }
        else
        {
            overview.Update(
                descDoc,
                targetDoc,
                learningDoc,
                overview.CoverImageDocumentId);
                
            _courseOverviewRepository.Update(overview);
        }

        return Result.Success;
    }
}
