using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;

public record GetCourseQuery(Guid CourseId) : IRequest<ErrorOr<Course>>;

public sealed class GetCourseQueryHandler : IRequestHandler<GetCourseQuery, ErrorOr<Course>>
{
    private readonly ICourseRepository _courseRepository;

    public GetCourseQueryHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<ErrorOr<Course>> Handle(GetCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        return course;
    }
}
