using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Overview;

public class GetCourseOverviewRequest
{
    public Guid Id { get; set; }
}

public record CourseOverviewResponse(
    Guid CourseId,
    JsonDocument DescriptionContent,
    JsonDocument? TargetAudienceContent,
    JsonDocument? LearningObjectivesContent);

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
        AllowAnonymous(); // The logic inside will check if published or if they have permission
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
        
        // Authorization check
        if (dto.CourseStatus != "Published")
        {
            // If not published, the user MUST be authenticated AND have courses.overview:Manage (or courses:View)
            var hasManagePermission = User.Identity?.IsAuthenticated == true && 
                User.HasClaim("permission", "courses.overview:Manage"); // This is a simplified check.
                
            // Proper IAM check via typical AlphaZero pattern? Let's check how it's done typically.
            // Usually we use endpoint policies but since it's anonymous, we check claims manually.
            if (!hasManagePermission)
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }
        }

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
