using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.View;

public class GetCourseViewRequest
{
    public Guid Id { get; set; } // bound from route /courses/{Id}/view
}

public record CourseViewPlanResponse(Guid Id, string Name);

public record CourseViewResourceResponse(
    Guid CurriculumResourceId,
    string? CourseAssetId,
    string? ResourceArn,
    string Type,
    int Order,
    double DurationSeconds);

public record CourseViewItemResponse(
    Guid Id,
    string Title,
    string Type,
    int Order,
    int BitIndex,
    List<CourseViewResourceResponse> Resources);

public record CourseViewSectionResponse(
    Guid Id,
    string Title,
    int Order,
    List<CourseViewItemResponse> Items);

public record CourseViewEnrollmentResponse(
    Guid EnrollmentId,
    string Status,
    double CompletionPercentage,
    List<Guid> CompletedItemIds);

public record CourseViewResponse(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    Guid SubjectId,
    string SubjectName,
    Guid? CoverImageDocumentId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int SectionCount,
    int ItemCount,
    double TotalDurationSeconds,
    List<CourseViewPlanResponse> Plans,
    List<CourseViewSectionResponse> Sections,
    CourseViewEnrollmentResponse? Enrollment);

public class GetCourseViewSummary : Summary<GetCourseViewEndpoint>
{
    public GetCourseViewSummary()
    {
        Summary = "Student course-detail view";
        Description = "Student-shaped course detail (#32): curriculum tree, overview-grade summary, subject name, plan summary. Published courses are viewable by anyone; the caller's own enrollment progress (enrollment id, status, completion percentage, completed items) is overlaid when they are enrolled. Resource identity uses real ARNs/ids — no edit-sensitive admin data (readiness internals, asset pool) and no empty metadata bucket.";
        Response<CourseViewResponse>(200, "Course view retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound) — also returned for drafts you may not see");
    }
}

public class GetCourseViewEndpoint : Endpoint<GetCourseViewRequest, CourseViewResponse>
{
    private readonly CoursesModule _module;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;

    public GetCourseViewEndpoint(CoursesModule module, IAuthorizationContextProvider authorizationContextProvider)
    {
        _module = module;
        _authorizationContextProvider = authorizationContextProvider;
    }

    public override void Configure()
    {
        Get("/courses/{Id}/view");
        // Permission-light browse surface (#32): Published courses are viewable by anyone;
        // visibility of unpublished content and the caller's own enrollment overlay are
        // resolved by the pre-request handler / query below. (Existing GET /courses/{Id}
        // admin shape and auth are untouched.)
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
        Summary(new GetCourseViewSummary());
    }

    public override async Task HandleAsync(GetCourseViewRequest req, CancellationToken ct)
    {
        // Only ever overlay the CALLER's own enrollment: the caller id comes from the
        // authenticated identity, never from a client-supplied parameter.
        Guid callerStudentId = Guid.Empty;
        var authResult = _authorizationContextProvider.Get(ct);
        if (!authResult.IsError && authResult.Value is not null)
        {
            callerStudentId = authResult.Value.Id;
        }
        var result = await _module.Send(new GetCourseViewQuery(req.Id, callerStudentId), ct);
        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var view = result.Value;
        var response = new CourseViewResponse(
            view.Id,
            view.Title,
            view.Description,
            view.Status,
            view.SubjectId,
            view.SubjectName,
            view.CoverImageDocumentId,
            view.CreatedAtUtc,
            view.UpdatedAtUtc,
            view.SectionCount,
            view.ItemCount,
            view.TotalDurationSeconds,
            view.Plans.Select(p => new CourseViewPlanResponse(p.Id, p.Name)).ToList(),
            view.Sections.Select(s => new CourseViewSectionResponse(
                s.Id,
                s.Title,
                s.Order,
                s.Items.Select(i => new CourseViewItemResponse(
                    i.Id,
                    i.Title,
                    i.Type,
                    i.Order,
                    i.BitIndex,
                    i.Resources.Select(r => new CourseViewResourceResponse(
                        r.CurriculumResourceId,
                        r.CourseAssetId,
                        r.ResourceArn,
                        r.Type,
                        r.Order,
                        r.DurationSeconds)).ToList())).ToList())).ToList(),
            view.Enrollment is null
                ? null
                : new CourseViewEnrollmentResponse(
                    view.Enrollment.EnrollmentId,
                    view.Enrollment.Status,
                    view.Enrollment.CompletionPercentage,
                    view.Enrollment.CompletedItemIds));

        await Send.OkAsync(response, ct);
    }
}
