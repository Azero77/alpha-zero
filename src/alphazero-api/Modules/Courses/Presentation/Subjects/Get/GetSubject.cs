using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Subjects.Get;

public record GetSubjectRequest
{
    public Guid Id { get; init; }
}

public class GetSubjectSummary : Summary<GetSubjectEndpoint>
{
    public GetSubjectSummary()
    {
        Summary = "Retrieves a subject by ID";
        Description = "Returns the details of a specific subject category.";
        Response<SubjectDto>(200, "Subject retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing subjects:View permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Subject not found");
    }
}

public class GetSubjectEndpoint : Endpoint<GetSubjectRequest, SubjectDto>
{
    private readonly CoursesModule _module;

    public GetSubjectEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/subjects/{id}");
        this.AccessControl("subjects:View", (req, tenantId) => ResourceArn.ForSubject(tenantId, req.Id));
        Description(d => d.WithTags("Subjects"));
        Summary(new GetSubjectSummary());
    }

    public override async Task HandleAsync(GetSubjectRequest req, CancellationToken ct)
    {
        var query = new GetSubjectQuery(req.Id);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
