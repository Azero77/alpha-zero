using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Get;

public record GetCourseRequest { public Guid Id { get; init; } }

public record CourseResponse(
    Guid Id,
    string Title,
    string? Description,
    Guid SubjectId,
    string Status,
    List<SectionResponse> Sections);

public record SectionResponse(
    Guid Id,
    string Title,
    int Order,
    List<ItemResponse> Items);

public record ItemResponse(
    Guid Id,
    string Title,
    string Type,
    int Order,
    int BitIndex,
    Guid ResourceId);

public class GetCourseSummary : Summary<GetCourseEndpoint>
{
    public GetCourseSummary()
    {
        Summary = "Retrieves a course by its ID";
        Description = "Returns the complete structure of a course, including all sections, lessons, and quizzes.";
        Response<CourseResponse>(200, "Course structure retrieved successfully");
        Response(404, "Course not found");
    }
}

public class GetCourseEndpoint : Endpoint<GetCourseRequest, CourseResponse>
{
    private readonly CoursesModule _module;

    public GetCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/{Id}");
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
        Summary(new GetCourseSummary());
    }
    public override async Task HandleAsync(GetCourseRequest req, CancellationToken ct)
    {
        var query = new GetCourseQuery(req.Id);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var course = result.Value;
        var response = new CourseResponse(
            course.Id,
            course.Title,
            course.Description,
            course.SubjectId,
            course.Status.ToString(),
            course.Sections.Select(s => new SectionResponse(
                s.Id,
                s.Title,
                s.Order,
                s.Items.Select(i => new ItemResponse(
                    i.Id,
                    i.Title,
                    i.GetType().Name.Replace("CourseSection", ""),
                    i.Order,
                    i.BitIndex,
                    i.ResourceId)).ToList())).ToList());

        await Send.OkAsync(response, ct);
    }
}
