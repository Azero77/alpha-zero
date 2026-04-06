using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;

public record CourseDto(
    Guid Id,
    string Title,
    string? Description,
    Guid SubjectId,
    string Status,
    List<SectionDto> Sections);

public record SectionDto(
    Guid Id,
    string Title,
    int Order,
    List<ItemDto> Items);

public record ItemDto(
    Guid Id,
    string Title,
    string Type,
    int Order,
    int BitIndex,
    Guid ResourceId);

public record GetCourseQuery(Guid CourseId) : IRequest<ErrorOr<CourseDto>>;

public sealed class GetCourseQueryHandler : IRequestHandler<GetCourseQuery, ErrorOr<CourseDto>>
{
    private readonly ICourseRepository _courseRepository;

    public GetCourseQueryHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<ErrorOr<CourseDto>> Handle(GetCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");

        var dto = new CourseDto(
            course.Id,
            course.Title,
            course.Description,
            course.SubjectId,
            course.Status.ToString(),
            course.Sections.OrderBy(s => s.Order).Select(s => new SectionDto(
                s.Id,
                s.Title,
                s.Order,
                s.Items.OrderBy(i => i.Order).Select(i => new ItemDto(
                    i.Id,
                    i.Title,
                    i.GetType().Name.Replace("CourseSection", ""),
                    i.Order,
                    i.BitIndex,
                    i.ResourceId)).ToList())).ToList());

        return dto;
    }
}
