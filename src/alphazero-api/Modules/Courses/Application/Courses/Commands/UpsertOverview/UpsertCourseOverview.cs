using ErrorOr;
using MediatR;
using System.Text.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.UpsertOverview;

public record UpsertCourseOverviewCommand(
    Guid CourseId,
    JsonElement DescriptionContent,
    JsonElement? TargetAudienceContent,
    JsonElement? LearningObjectivesContent) : IRequest<ErrorOr<Success>>;

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

        // Map JsonElement to JsonDocument safely
        var descDoc = JsonDocument.Parse(request.DescriptionContent.GetRawText());
        var targetDoc = request.TargetAudienceContent.HasValue ? JsonDocument.Parse(request.TargetAudienceContent.Value.GetRawText()) : null;
        var learningDoc = request.LearningObjectivesContent.HasValue ? JsonDocument.Parse(request.LearningObjectivesContent.Value.GetRawText()) : null;

        if (overview is null)
        {
            overview = CourseOverview.Create(
                Guid.NewGuid(),
                tenantId.Value,
                request.CourseId,
                descDoc,
                targetDoc,
                learningDoc,
                null); // CoverImageDocumentId is omitted in Ticket #20

            _courseOverviewRepository.Add(overview);
        }
        else
        {
            overview.Update(
                descDoc,
                targetDoc,
                learningDoc,
                overview.CoverImageDocumentId); // Preserve cover image
                
            _courseOverviewRepository.Update(overview);
        }

        return Result.Success;
    }
}
