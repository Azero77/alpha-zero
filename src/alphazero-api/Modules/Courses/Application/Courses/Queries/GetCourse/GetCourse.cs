using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using MediatR;
using System.Text.Json;

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
    string Type, // Populated with MainType (e.g. "Video", "Quiz")
    int Order,
    int BitIndex,
    List<ResourceDto> Resources); // Ordered by ResourceDto.Order

public record ResourceDto(
    string Arn,
    string Type, // "Primary" or "Auxiliary"
    int Order,
    JsonElement Metadata);

public record GetCourseQuery(Guid CourseId) : IRequest<ErrorOr<CourseDto>>;

public sealed class GetCourseQueryHandler : IRequestHandler<GetCourseQuery, ErrorOr<CourseDto>>
{
    private readonly ICourseQueryService _courseQueryService;

    public GetCourseQueryHandler(ICourseQueryService courseQueryService)
    {
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<CourseDto>> Handle(GetCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await _courseQueryService.GetCourseByIdAsync(request.CourseId, cancellationToken);
        if (course is null) return Error.NotFound("Course.NotFound", "Course not found.");
        return course;
    }
}
