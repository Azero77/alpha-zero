using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseReadiness;

public record GetCourseReadinessQuery(Guid CourseId) : IQuery<CourseReadiness>;

public class GetCourseReadinessQueryValidator : AbstractValidator<GetCourseReadinessQuery>
{
    public GetCourseReadinessQueryValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public sealed class GetCourseReadinessQueryHandler : IRequestHandler<GetCourseReadinessQuery, ErrorOr<CourseReadiness>>
{
    private readonly ICourseQueryService _courseQueryService;

    public GetCourseReadinessQueryHandler(ICourseQueryService courseQueryService)
    {
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<CourseReadiness>> Handle(GetCourseReadinessQuery request, CancellationToken cancellationToken)
    {
        var readiness = await _courseQueryService.GetCourseReadinessAsync(request.CourseId, cancellationToken);
        if (readiness is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        return readiness;
    }
}
