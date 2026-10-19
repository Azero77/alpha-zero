using AlphaZero.Modules.Courses.Application.Subjects.Commands.Delete;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Subjects.Delete;

public record DeleteSubjectRequest
{
    public Guid Id { get; init; }
}

public class DeleteSubjectSummary : Summary<DeleteSubjectEndpoint>
{
    public DeleteSubjectSummary()
    {
        Summary = "Soft-deletes a subject";
        Description = "Retires a subject from pickers and subject lists. Refused with Subject.InUse while any non-deleted course in the tenant still references it.";
        Response(204, "Subject deleted successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (id empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing subjects:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Subject not found (Subject.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Subject.InUse - a live course references the subject)");
    }
}

public class DeleteSubjectEndpoint : Endpoint<DeleteSubjectRequest>
{
    private readonly CoursesModule _module;

    public DeleteSubjectEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/courses/subjects/{Id}");
        this.AccessControl("subjects:Edit", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Subjects"));
        Summary(new DeleteSubjectSummary());
    }

    public override async Task HandleAsync(DeleteSubjectRequest req, CancellationToken ct)
    {
        var command = new DeleteSubjectCommand(req.Id);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
