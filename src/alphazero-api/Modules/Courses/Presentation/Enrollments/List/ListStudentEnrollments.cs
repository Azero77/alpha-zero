using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetStudentEnrollments;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollments.List;

public record ListStudentEnrollmentsRequest
{
    public Guid? StudentId { get; init; }

    public int Page { get; init; } = 1;

    public int PerPage { get; init; } = 10;
}

public class ListStudentEnrollmentsSummary : Summary<ListStudentEnrollmentsEndpoint>
{
    public ListStudentEnrollmentsSummary()
    {
        Summary = "Lists a student's enrollments";
        Description = "Paged, tenant-scoped listing of all of a student's enrollments (any status) with course reference, status, completion percentage and enrollment date.";
        Response<PagedResult<EnrollmentDto>>(200, "Enrollments retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (StudentId empty, paging bounds)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing enrollments:List permission)");
    }
}

public class ListStudentEnrollmentsEndpoint : Endpoint<ListStudentEnrollmentsRequest, PagedResult<EnrollmentDto>>
{
    private readonly CoursesModule _module;

    public ListStudentEnrollmentsEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/enrollments");
        this.AccessControl("enrollments:List", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Enrollment"));
        Summary(new ListStudentEnrollmentsSummary());
    }

    public override async Task HandleAsync(ListStudentEnrollmentsRequest req, CancellationToken ct)
    {
        var query = new GetStudentEnrollmentsQuery(
            req.StudentId ?? Guid.Empty, req.Page, req.PerPage);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
