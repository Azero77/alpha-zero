using AlphaZero.Modules.Courses.Application.Enrollements.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Presentation;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Enrollements.CompleteItem;

public record CompleteItemRequest
{
    public Guid EnrollmentId { get; init; }
    public int BitIndex { get; init; }
}

public class CompleteItemSummary : Summary<CompleteItemEndpoint>
{
    public CompleteItemSummary()
    {
        Summary = "Marks a course item as completed";
        Description = "Updates the student's progress bitmask for the specified item bit index.";
        Response(204, "Item marked as completed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing enrollments:Complete permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Not found (Enrollment.NotFound, Course.Item)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Enrollement.Status - cannot complete items in inactive enrollment)");
    }
}

public class CompleteItemEndpoint : Endpoint<CompleteItemRequest>
{
    private readonly CoursesModule _module;

    public CompleteItemEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/enrollements/{EnrollmentId}/complete");
        this.AccessControl("enrollments:Complete", (req, tenantId) => ResourceArn.ForEnrollment(tenantId, req.EnrollmentId));
        Description(d => d.WithTags("Enrollement"));
        Summary(new CompleteItemSummary());
    }

    public override async Task HandleAsync(CompleteItemRequest req, CancellationToken ct)
    {
        var command = new CompleteItemCommand(req.EnrollmentId, req.BitIndex);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
