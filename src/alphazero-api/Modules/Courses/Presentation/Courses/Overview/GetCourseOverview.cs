using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Overview;

public class GetCourseOverviewRequest
{
    public Guid Id { get; set; }
}

public record CourseOverviewResponse(
    Guid CourseId,
    string DescriptionContent,
    string? TargetAudienceContent,
    string? LearningObjectivesContent);

public class GetCourseOverviewEndpoint : Endpoint<GetCourseOverviewRequest, CourseOverviewResponse>
{
    private readonly CoursesModule _module;

    public GetCourseOverviewEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/{Id}/overview");
        AllowAnonymous(); // Published courses can be viewed by anyone; unpublished courses are guarded in PreRequestHandler
        Description(d => d.WithTags("Courses"));
        Summary(s =>
        {
            s.Summary = "Gets a course overview";
            s.Description = "Retrieves the rich text blocks. Published courses can be viewed by anyone.";
            s.Responses[200] = "Successfully retrieved course overview";
            s.Responses[404] = "Course or overview not found";
            s.Responses[403] = "Forbidden";
        });
    }

    public override async Task HandleAsync(GetCourseOverviewRequest req, CancellationToken ct)
    {
        var query = new GetCourseOverviewQuery(req.Id);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var dto = result.Value;

        // Set Cache Headers
        HttpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(300)
        };
        HttpContext.Response.Headers.ETag = dto.ETag;

        // ETag conditional request
        if (HttpContext.Request.Headers.IfNoneMatch.ToString() == dto.ETag)
        {
            HttpContext.Response.StatusCode = 304;
            return;
        }

        var response = new CourseOverviewResponse(
            dto.CourseId,
            dto.DescriptionContent,
            dto.TargetAudienceContent,
            dto.LearningObjectivesContent);

        await Send.OkAsync(response, ct);
    }
}
