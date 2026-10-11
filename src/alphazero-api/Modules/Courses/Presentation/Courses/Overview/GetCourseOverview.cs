using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

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
            var policyEvaluator = HttpContext.RequestServices.GetService<IPolicyEvaluatorService>();
            var authContextFactory = HttpContext.RequestServices.GetService<IAuthorizationContextFactory>();
            var tenantProvider = HttpContext.RequestServices.GetService<AlphaZero.Shared.Infrastructure.Tenats.ITenantProvider>();
            
            if (policyEvaluator != null && authContextFactory != null && tenantProvider != null)
            {
                var tenantId = tenantProvider.GetTenant();
                if (tenantId == null || !User.Identity!.IsAuthenticated)
                {
                    HttpContext.Response.StatusCode = 404;
                    return;
                }

                var idStr = User.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier)?.Value;
                var authSchemeStr = User.Claims.FirstOrDefault(c => c.Type == "auth_method")?.Value ?? "Principal";
                
                if (string.IsNullOrEmpty(idStr) || !Enum.TryParse<AuthenticationMethod>(authSchemeStr, true, out var authMethod))
                {
                    HttpContext.Response.StatusCode = 404;
                    return;
                }

                var arn = ResourceArn.ForCourse(tenantId.Value, req.Id);
                var authContextResult = await authContextFactory.Create("courses.overview:Manage", arn, authMethod, idStr, ct);
                
                if (authContextResult.IsError)
                {
                    HttpContext.Response.StatusCode = 404;
                    return;
                }
                
                var authResult = await policyEvaluator.Authorize(authContextResult.Value);
                
                if (authResult.IsError)
                {
                    HttpContext.Response.StatusCode = 404;
                    return;
                }
            }
            else 
            {
                // Fallback if services not injected correctly
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
